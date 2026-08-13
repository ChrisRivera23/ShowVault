using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ShowVault.Agent.Recovery;

internal enum RestoreRacePoint
{
    DestinationFileOpened,
    AdoptionDirectoryOpened,
    StagingTreeValidated,
    PublishedFileHashStarted
}

internal interface IRestoreRaceProbe
{
    void Reached(RestoreRacePoint point, string relativePath);
}

internal sealed class StableDirectoryTree : IDisposable
{
    private readonly string _path;
    private readonly SafeFileHandle _handle;
    private readonly IReadOnlyList<SafeFileHandle> _ancestorGuards;

    private StableDirectoryTree(
        string path,
        SafeFileHandle handle,
        IReadOnlyList<SafeFileHandle>? ancestorGuards = null)
    {
        _path = path;
        _handle = handle;
        _ancestorGuards = ancestorGuards ?? [];
    }

    public string Path => _path;

    public StableDirectoryTree Duplicate()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new StableDirectoryTree(_path, UnixNative.Duplicate(_handle));
        }

        var handle = WindowsNative.Duplicate(_handle);
        try
        {
            return new StableDirectoryTree(
                _path,
                handle,
                DuplicateWindowsGuards(includeCurrent: false));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public bool HasSameIdentity(StableDirectoryTree other) => OperatingSystem.IsWindows()
        ? WindowsNative.SameIdentity(_handle, other._handle)
        : UnixNative.SameIdentity(_handle, other._handle);

    public bool HasSameVolume(StableDirectoryTree other) => OperatingSystem.IsWindows()
        ? WindowsNative.SameVolume(_handle, other._handle)
        : UnixNative.SameDevice(_handle, other._handle);

    public static StableDirectoryTree Open(string path)
    {
        var normalized = System.IO.Path.TrimEndingDirectorySeparator(
            System.IO.Path.GetFullPath(path));
        return OperatingSystem.IsWindows()
            ? new StableDirectoryTree(normalized, WindowsNative.OpenDirectory(normalized, canDelete: false))
            : new StableDirectoryTree(normalized, UnixNative.OpenDirectory(normalized));
    }

    private static string NormalizeReadOnlySourcePath(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return path;
        }

        foreach (var alias in new[] { "/etc", "/tmp", "/var" })
        {
            if (string.Equals(path, alias, StringComparison.Ordinal) ||
                path.StartsWith($"{alias}/", StringComparison.Ordinal))
            {
                return $"/private{path}";
            }
        }

        return path;
    }

    public static StableDirectoryTree OpenReadOnlyNoFollowPath(string path)
    {
        var normalized = NormalizeReadOnlySourcePath(
            System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path)));
        var pathRoot = System.IO.Path.GetPathRoot(normalized);
        if (string.IsNullOrEmpty(pathRoot))
        {
            throw new IOException("Source root is not fully qualified.");
        }

        StableDirectoryTree? current = OperatingSystem.IsWindows()
            ? new StableDirectoryTree(
                pathRoot,
                WindowsNative.OpenDirectoryReadOnly(pathRoot))
            : new StableDirectoryTree(pathRoot, UnixNative.OpenDirectory(pathRoot));
        try
        {
            var relativePath = System.IO.Path.GetRelativePath(pathRoot, normalized);
            if (relativePath == ".")
            {
                var result = current;
                current = null;
                return result;
            }

            foreach (var segment in relativePath.Split(
                         [System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var next = current.OpenDirectoryReadOnly(segment);
                current.Dispose();
                current = next;
            }

            var opened = current;
            current = null;
            return opened;
        }
        finally
        {
            current?.Dispose();
        }
    }

    public StableDirectoryTree OpenDirectory(string name)
    {
        ValidateName(name);
        var path = System.IO.Path.Combine(_path, name);
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
        }

        if (OperatingSystem.IsWindows())
        {
            return CreateWindowsChild(path, WindowsNative.OpenDirectory(path, canDelete: true));
        }

        return new StableDirectoryTree(path, UnixNative.OpenDirectoryAt(_handle, name));
    }

    public StableDirectoryTree OpenDirectoryReadOnly(string name)
    {
        ValidateName(name);
        var path = System.IO.Path.Combine(_path, name);
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
            return CreateWindowsChild(path, WindowsNative.OpenDirectoryReadOnly(path));
        }

        return new StableDirectoryTree(path, UnixNative.OpenDirectoryAt(_handle, name));
    }

    public StableDirectoryTree CreateDirectory(string name)
    {
        ValidateName(name);
        var path = System.IO.Path.Combine(_path, name);
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
            Directory.CreateDirectory(path);
            return CreateWindowsChild(path, WindowsNative.OpenDirectory(path, canDelete: true));
        }

        UnixNative.CreateDirectoryAt(_handle, name);
        return new StableDirectoryTree(path, UnixNative.OpenDirectoryAt(_handle, name));
    }

    public StableDirectoryTree GetOrCreateDirectory(string name)
    {
        try
        {
            return OpenDirectory(name);
        }
        catch (IOException)
        {
            return CreateDirectory(name);
        }
    }

    public FileStream CreateFile(string name)
    {
        ValidateName(name);
        var path = System.IO.Path.Combine(_path, name);
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
        }

        var handle = OperatingSystem.IsWindows()
            ? WindowsNative.CreateRegularFile(path)
            : UnixNative.CreateRegularFileAt(_handle, name);
        return new FileStream(handle, FileAccess.ReadWrite, 65_536, isAsync: false);
    }

    public FileStream OpenRegularFile(string name)
    {
        ValidateName(name);
        var path = System.IO.Path.Combine(_path, name);
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
        }

        var handle = OperatingSystem.IsWindows()
            ? WindowsNative.OpenRegularFile(path)
            : UnixNative.OpenRegularFileAt(_handle, name);
        return new FileStream(handle, FileAccess.Read, 65_536, isAsync: false);
    }

    public IReadOnlyList<string> EnumerateNames()
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
            return Directory.EnumerateFileSystemEntries(_path)
                .Select(path => System.IO.Path.GetFileName(path) ??
                    throw new IOException("Restore directory entry name is invalid."))
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        return UnixNative.EnumerateNames(_handle);
    }

    public bool IsSameDirectoryAt(StableDirectoryTree parent, string name)
    {
        ValidateName(name);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var currentIdentity = WindowsNative.OpenDirectoryForIdentity(
                    System.IO.Path.Combine(parent._path, name));
                return WindowsNative.SameIdentity(_handle, currentIdentity);
            }

            using var current = parent.OpenDirectoryReadOnly(name);
            return UnixNative.SameIdentity(_handle, current._handle);
        }
        catch (IOException)
        {
            return false;
        }
    }

    public bool IsSameFileAt(string name, SafeFileHandle expected)
    {
        ValidateName(name);
        try
        {
            using var current = OperatingSystem.IsWindows()
                ? WindowsNative.OpenRegularFile(System.IO.Path.Combine(_path, name))
                : UnixNative.OpenRegularFileAt(_handle, name);
            return OperatingSystem.IsWindows()
                ? WindowsNative.SameIdentity(expected, current)
                : UnixNative.SameIdentity(expected, current);
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void RenameChild(
        string sourceName,
        StableDirectoryTree expected,
        string destinationName)
    {
        ValidateName(sourceName);
        ValidateName(destinationName);
        if (!expected.IsSameDirectoryAt(this, sourceName))
        {
            throw new IOException("Restore directory identity changed before publication.");
        }

        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
            WindowsNative.Rename(expected._handle, _handle, destinationName);
            return;
        }

        UnixNative.RenameAt(_handle, sourceName, _handle, destinationName);
    }

    public void MoveDirectoryChildTo(
        string sourceName,
        StableDirectoryTree expected,
        StableDirectoryTree destination,
        string destinationName)
    {
        ValidateName(sourceName);
        ValidateName(destinationName);
        if (!expected.IsSameDirectoryAt(this, sourceName))
        {
            throw new IOException("Restore directory identity changed before placement.");
        }

        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
            WindowsNative.EnsureSameDirectory(destination._handle, destination._path);
            WindowsNative.Rename(expected._handle, destination._handle, destinationName);
        }
        else
        {
            UnixNative.RenameAt(_handle, sourceName, destination._handle, destinationName);
        }

        if (!expected.IsSameDirectoryAt(destination, destinationName))
        {
            throw new IOException("Restore directory identity changed during placement.");
        }
    }

    public void MoveChildTo(
        string sourceName,
        SafeFileHandle expected,
        StableDirectoryTree destination,
        string destinationName)
    {
        ValidateName(sourceName);
        ValidateName(destinationName);
        if (!IsSameFileAt(sourceName, expected))
        {
            throw new IOException("Restore staging file identity changed before placement.");
        }

        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
            WindowsNative.EnsureSameDirectory(destination._handle, destination._path);
            WindowsNative.Rename(expected, destination._handle, destinationName);
        }
        else
        {
            UnixNative.RenameAt(_handle, sourceName, destination._handle, destinationName);
        }

        if (!destination.IsSameFileAt(destinationName, expected))
        {
            throw new IOException("Restore staging file identity changed during placement.");
        }
    }

    public void DeleteChildTreeIfSame(string name, StableDirectoryTree expected)
    {
        ValidateName(name);
        if (!expected.IsSameDirectoryAt(this, name))
        {
            return;
        }

        expected.DeleteContents();
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.MarkDelete(expected._handle);
            expected.Dispose();
        }
        else
        {
            UnixNative.UnlinkAt(_handle, name, UnixAtRemoveDirectoryFlag);
        }
    }

    private void DeleteContents()
    {
        foreach (var name in EnumerateNames())
        {
            try
            {
                using var child = OpenDirectory(name);
                child.DeleteContents();
                if (OperatingSystem.IsWindows())
                {
                    WindowsNative.MarkDelete(child._handle);
                    child.Dispose();
                }
                else
                {
                    UnixNative.UnlinkAt(_handle, name, UnixAtRemoveDirectoryFlag);
                }
            }
            catch (IOException)
            {
                if (OperatingSystem.IsWindows())
                {
                    WindowsNative.DeleteEntry(System.IO.Path.Combine(_path, name));
                }
                else
                {
                    UnixNative.UnlinkAt(_handle, name, 0);
                }
            }
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
        foreach (var guard in _ancestorGuards)
        {
            guard.Dispose();
        }
    }

    private StableDirectoryTree CreateWindowsChild(string path, SafeFileHandle handle)
    {
        try
        {
            return new StableDirectoryTree(
                path,
                handle,
                DuplicateWindowsGuards(includeCurrent: true));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private IReadOnlyList<SafeFileHandle> DuplicateWindowsGuards(bool includeCurrent)
    {
        var duplicates = new List<SafeFileHandle>();
        try
        {
            duplicates.AddRange(_ancestorGuards.Select(WindowsNative.Duplicate));
            if (includeCurrent)
            {
                duplicates.Add(WindowsNative.Duplicate(_handle));
            }

            return duplicates;
        }
        catch
        {
            foreach (var duplicate in duplicates)
            {
                duplicate.Dispose();
            }

            throw;
        }
    }

    internal static int ResolveUnixAtRemoveDirectoryFlag(bool isMacOS, bool isLinux)
    {
        if (isMacOS == isLinux)
        {
            throw new PlatformNotSupportedException(
                "Restore directory cleanup is supported only on macOS and Linux.");
        }

        return isMacOS ? 0x80 : 0x200;
    }

    private static int UnixAtRemoveDirectoryFlag => ResolveUnixAtRemoveDirectoryFlag(
        OperatingSystem.IsMacOS(),
        OperatingSystem.IsLinux());

    private static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name) || name is "." or ".." ||
            name.IndexOfAny([System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new InvalidOperationException("Restore path segment is invalid.");
        }
    }

    private static class UnixNative
    {
        private const int ReadOnly = 0;
        private const int ReadWrite = 2;
        private const uint DirectoryMode = 0x1C0; // 0700
        private const uint FileMode = 0x180; // 0600

        public static SafeFileHandle OpenDirectory(string path) =>
            Wrap(open(path, ReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag));

        public static SafeFileHandle OpenDirectoryAt(SafeFileHandle parent, string name) =>
            Wrap(openat(Fd(parent), name, ReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag, 0));

        public static SafeFileHandle Duplicate(SafeFileHandle handle) => Wrap(dup(Fd(handle)));

        public static void CreateDirectoryAt(SafeFileHandle parent, string name)
        {
            if (mkdirat(Fd(parent), name, DirectoryMode) != 0)
            {
                throw Error("Could not create a restore directory.");
            }
        }

        public static SafeFileHandle CreateRegularFileAt(SafeFileHandle parent, string name)
        {
            var handle = Wrap(openat(
                Fd(parent),
                name,
                ReadWrite | CreateFlag | ExclusiveFlag | NoFollowFlag | CloseOnExecFlag,
                FileMode));
            if (fchmod(Fd(handle), FileMode) != 0)
            {
                handle.Dispose();
                throw Error("Could not secure a restore file.");
            }

            return handle;
        }

        public static SafeFileHandle OpenRegularFileAt(SafeFileHandle parent, string name)
        {
            var handle = Wrap(openat(
                Fd(parent),
                name,
                ReadOnly | NoFollowFlag | NonBlockFlag | CloseOnExecFlag,
                0));
            if (!IsRegular(handle))
            {
                handle.Dispose();
                throw new IOException("Restore tree entries must be regular files.");
            }

            return handle;
        }

        public static IReadOnlyList<string> EnumerateNames(SafeFileHandle directory)
        {
            var enumerationHandle = openat(
                Fd(directory),
                ".",
                ReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
                0);
            if (enumerationHandle < 0)
            {
                throw Error("Could not open a restore directory for enumeration.");
            }

            var stream = fdopendir(enumerationHandle);
            if (stream == IntPtr.Zero)
            {
                close(enumerationHandle);
                throw Error("Could not enumerate a restore directory.");
            }

            var names = new List<string>();
            try
            {
                while (true)
                {
                    Marshal.SetLastPInvokeError(0);
                    var entry = readdir(stream);
                    if (entry == IntPtr.Zero)
                    {
                        if (Marshal.GetLastPInvokeError() != 0)
                        {
                            throw Error("Could not enumerate a restore directory.");
                        }

                        break;
                    }

                    var nameOffset = OperatingSystem.IsMacOS() ? 21 : 19;
                    var name = Marshal.PtrToStringUTF8(entry + nameOffset)
                        ?? throw new IOException("Restore directory entry name is invalid.");
                    if (name is not "." and not "..")
                    {
                        names.Add(name);
                    }
                }
            }
            finally
            {
                closedir(stream);
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        public static bool SameIdentity(SafeFileHandle left, SafeFileHandle right)
        {
            var leftIdentity = Identity(left);
            var rightIdentity = Identity(right);
            return leftIdentity == rightIdentity;
        }

        public static bool SameDevice(SafeFileHandle left, SafeFileHandle right) =>
            Identity(left).Device == Identity(right).Device;

        public static void RenameAt(
            SafeFileHandle sourceParent,
            string source,
            SafeFileHandle destinationParent,
            string destination)
        {
            if (renameat(
                Fd(sourceParent),
                source,
                Fd(destinationParent),
                destination) != 0)
            {
                throw Error("Could not publish the restored target.");
            }
        }

        public static void UnlinkAt(SafeFileHandle parent, string name, int flags)
        {
            if (unlinkat(Fd(parent), name, flags) != 0)
            {
                throw Error("Could not clean the restore staging tree.");
            }
        }

        private static (ulong Device, ulong Inode) Identity(SafeFileHandle handle)
        {
            var buffer = Marshal.AllocHGlobal(256);
            try
            {
                if (fstat(Fd(handle), buffer) != 0)
                {
                    throw Error("Could not inspect a restore filesystem handle.");
                }

                return OperatingSystem.IsMacOS()
                    ? ((uint)Marshal.ReadInt32(buffer), (ulong)Marshal.ReadInt64(buffer, 8))
                    : ((ulong)Marshal.ReadInt64(buffer), (ulong)Marshal.ReadInt64(buffer, 8));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool IsRegular(SafeFileHandle handle)
        {
            var buffer = Marshal.AllocHGlobal(256);
            try
            {
                if (fstat(Fd(handle), buffer) != 0)
                {
                    throw Error("Could not inspect a restore file handle.");
                }

                var mode = OperatingSystem.IsMacOS()
                    ? (uint)(ushort)Marshal.ReadInt16(buffer, 4)
                    : (uint)Marshal.ReadInt32(buffer, 24);
                return (mode & 0xF000) == 0x8000;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static int Fd(SafeFileHandle handle) => checked((int)handle.DangerousGetHandle());

        private static SafeFileHandle Wrap(int descriptor)
        {
            if (descriptor < 0)
            {
                throw Error("Restore filesystem operation was refused.");
            }

            return new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        }

        private static IOException Error(string message) =>
            new(message, new Win32Exception(Marshal.GetLastPInvokeError()));

        private static int DirectoryFlag => OperatingSystem.IsMacOS() ? 0x100000 : 0x10000;
        private static int NoFollowFlag => OperatingSystem.IsMacOS() ? 0x100 : 0x20000;
        private static int CloseOnExecFlag => OperatingSystem.IsMacOS() ? 0x1000000 : 0x80000;
        private static int NonBlockFlag => OperatingSystem.IsMacOS() ? 0x4 : 0x800;
        private static int CreateFlag => OperatingSystem.IsMacOS() ? 0x200 : 0x40;
        private static int ExclusiveFlag => OperatingSystem.IsMacOS() ? 0x800 : 0x80;

        [DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags);
        [DllImport("libc", SetLastError = true)] private static extern int openat(int fd, string path, int flags, uint mode);
        [DllImport("libc", SetLastError = true)] private static extern int mkdirat(int fd, string path, uint mode);
        [DllImport("libc", SetLastError = true)] private static extern int renameat(int oldFd, string oldPath, int newFd, string newPath);
        [DllImport("libc", SetLastError = true)] private static extern int unlinkat(int fd, string path, int flags);
        [DllImport("libc", SetLastError = true)] private static extern int fstat(int fd, IntPtr stat);
        [DllImport("libc", SetLastError = true)] private static extern int fchmod(int fd, uint mode);
        [DllImport("libc", SetLastError = true)] private static extern int dup(int fd);
        [DllImport("libc", SetLastError = true)] private static extern int close(int fd);
        [DllImport("libc", SetLastError = true)] private static extern IntPtr fdopendir(int fd);
        [DllImport("libc", SetLastError = true)] private static extern IntPtr readdir(IntPtr directory);
        [DllImport("libc", SetLastError = true)] private static extern int closedir(IntPtr directory);
    }

    private static class WindowsNative
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint Delete = 0x00010000;
        private const uint ShareRead = 1;
        private const uint ShareWrite = 2;
        private const uint ShareDelete = 4;
        private const uint OpenExisting = 3;
        private const uint CreateNew = 1;
        private const uint BackupSemantics = 0x02000000;
        private const uint OpenReparsePoint = 0x00200000;
        private const int FileRenameInfo = 3;
        private const int FileDispositionInfo = 4;
        private const int FileAttributeTagInfo = 9;
        private const int FileIdInfo = 18;

        public static SafeFileHandle OpenDirectory(string path, bool canDelete)
        {
            var access = GenericRead | GenericWrite | (canDelete ? Delete : 0);
            var handle = Open(
                path,
                access,
                ShareRead | ShareWrite,
                OpenExisting,
                BackupSemantics | OpenReparsePoint);
            EnsureNotReparse(handle);
            return handle;
        }

        public static SafeFileHandle OpenDirectoryReadOnly(string path)
        {
            var handle = Open(
                path,
                GenericRead,
                ShareRead | ShareWrite | ShareDelete,
                OpenExisting,
                BackupSemantics | OpenReparsePoint);
            EnsureNotReparse(handle);
            return handle;
        }

        public static SafeFileHandle Duplicate(SafeFileHandle handle)
        {
            if (!DuplicateHandle(
                GetCurrentProcess(),
                handle,
                GetCurrentProcess(),
                out var duplicate,
                0,
                false,
                2))
            {
                throw new IOException(
                    "Could not duplicate restore directory identity.",
                    new Win32Exception(Marshal.GetLastPInvokeError()));
            }

            return duplicate;
        }

        public static SafeFileHandle CreateRegularFile(string path) =>
            Open(
                path,
                GenericRead | GenericWrite | Delete,
                ShareRead | ShareDelete,
                CreateNew,
                OpenReparsePoint);

        public static SafeFileHandle OpenRegularFile(string path)
        {
            var handle = Open(
                path,
                GenericRead,
                ShareRead | ShareWrite | ShareDelete,
                OpenExisting,
                OpenReparsePoint);
            EnsureNotReparse(handle);
            return handle;
        }

        public static void EnsureSameDirectory(SafeFileHandle expected, string path)
        {
            using var current = OpenDirectoryForIdentity(path);
            if (!SameIdentity(expected, current))
            {
                throw new IOException("Restore directory identity changed.");
            }
        }

        public static bool SameIdentity(SafeFileHandle left, SafeFileHandle right)
        {
            var leftInfo = GetId(left);
            var rightInfo = GetId(right);
            return leftInfo.VolumeSerialNumber == rightInfo.VolumeSerialNumber &&
                leftInfo.FileIdLow == rightInfo.FileIdLow &&
                leftInfo.FileIdHigh == rightInfo.FileIdHigh;
        }

        public static bool SameVolume(SafeFileHandle left, SafeFileHandle right) =>
            GetId(left).VolumeSerialNumber == GetId(right).VolumeSerialNumber;

        public static void Rename(
            SafeFileHandle source,
            SafeFileHandle destinationParent,
            string destinationName)
        {
            var nameBytes = checked(destinationName.Length * sizeof(char));
            var rootOffset = IntPtr.Size;
            var lengthOffset = checked(rootOffset + IntPtr.Size);
            var nameOffset = checked(lengthOffset + sizeof(uint));
            var bufferSize = checked(nameOffset + nameBytes + sizeof(char));
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                Marshal.Copy(new byte[bufferSize], 0, buffer, bufferSize);
                Marshal.WriteIntPtr(buffer, rootOffset, destinationParent.DangerousGetHandle());
                Marshal.WriteInt32(buffer, lengthOffset, nameBytes);
                Marshal.Copy(destinationName.ToCharArray(), 0, buffer + nameOffset, destinationName.Length);
                if (!SetFileInformationByHandle(
                    source,
                    FileRenameInfo,
                    buffer,
                    (uint)bufferSize))
                {
                    throw Error("Could not publish the restored target.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static void MarkDelete(SafeFileHandle handle)
        {
            var delete = Marshal.AllocHGlobal(1);
            try
            {
                Marshal.WriteByte(delete, 1);
                if (!SetFileInformationByHandle(
                    handle,
                    FileDispositionInfo,
                    delete,
                    1))
                {
                    throw Error("Could not clean the restore staging tree.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(delete);
            }
        }

        public static void DeleteEntry(string path)
        {
            using var handle = Open(
                path,
                Delete,
                ShareRead | ShareWrite | ShareDelete,
                OpenExisting,
                BackupSemantics | OpenReparsePoint);
            MarkDelete(handle);
        }

        public static SafeFileHandle OpenDirectoryForIdentity(string path)
        {
            var handle = Open(
                path,
                GenericRead,
                ShareRead | ShareWrite | ShareDelete,
                OpenExisting,
                BackupSemantics | OpenReparsePoint);
            EnsureNotReparse(handle);
            return handle;
        }

        private static SafeFileHandle Open(
            string path,
            uint access,
            uint shareMode,
            uint disposition,
            uint flags)
        {
            var handle = CreateFileW(
                path,
                access,
                shareMode,
                IntPtr.Zero,
                disposition,
                flags,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                throw new IOException(
                    "Restore filesystem operation was refused.",
                    new Win32Exception(Marshal.GetLastPInvokeError()));
            }

            return handle;
        }

        private static IOException Error(string message) =>
            new(message, new Win32Exception(Marshal.GetLastPInvokeError()));

        private static void EnsureNotReparse(SafeFileHandle handle)
        {
            if (!GetFileInformationByHandleEx(
                handle,
                FileAttributeTagInfo,
                out FileAttributeTagInformation info,
                Marshal.SizeOf<FileAttributeTagInformation>()) ||
                (info.FileAttributes & (uint)FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Restore paths cannot traverse reparse points.");
            }
        }

        private static FileIdInformation GetId(SafeFileHandle handle)
        {
            if (!GetFileInformationByHandleEx(
                handle,
                FileIdInfo,
                out FileIdInformation info,
                Marshal.SizeOf<FileIdInformation>()))
            {
                throw new IOException("Could not inspect restore directory identity.");
            }

            return info;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileAttributeTagInformation
        {
            public uint FileAttributes;
            public uint ReparseTag;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileIdInformation
        {
            public ulong VolumeSerialNumber;
            public ulong FileIdLow;
            public ulong FileIdHigh;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle handle,
            int informationClass,
            out FileAttributeTagInformation information,
            int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle handle,
            int informationClass,
            out FileIdInformation information,
            int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(
            SafeFileHandle handle,
            int informationClass,
            IntPtr information,
            uint size);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateHandle(
            IntPtr sourceProcess,
            SafeFileHandle sourceHandle,
            IntPtr targetProcess,
            out SafeFileHandle targetHandle,
            uint desiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            uint options);
    }
}
