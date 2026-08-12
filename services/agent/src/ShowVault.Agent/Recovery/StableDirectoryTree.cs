using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ShowVault.Agent.Recovery;

internal enum RestoreRacePoint
{
    DestinationFileOpened,
    AdoptionDirectoryOpened
}

internal interface IRestoreRaceProbe
{
    void Reached(RestoreRacePoint point, string relativePath);
}

internal sealed class StableDirectoryTree : IDisposable
{
    private const int UnixAtRemovedir = 0x80;
    private readonly string _path;
    private readonly SafeFileHandle _handle;

    private StableDirectoryTree(string path, SafeFileHandle handle)
    {
        _path = path;
        _handle = handle;
    }

    public string Path => _path;

    public StableDirectoryTree Duplicate() => new(
        _path,
        OperatingSystem.IsWindows()
            ? WindowsNative.Duplicate(_handle)
            : UnixNative.Duplicate(_handle));

    public static StableDirectoryTree Open(string path)
    {
        var normalized = System.IO.Path.TrimEndingDirectorySeparator(
            System.IO.Path.GetFullPath(path));
        return OperatingSystem.IsWindows()
            ? new StableDirectoryTree(normalized, WindowsNative.OpenDirectory(normalized))
            : new StableDirectoryTree(normalized, UnixNative.OpenDirectory(normalized));
    }

    public StableDirectoryTree OpenDirectory(string name)
    {
        ValidateName(name);
        var path = System.IO.Path.Combine(_path, name);
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
        }

        return OperatingSystem.IsWindows()
            ? new StableDirectoryTree(path, WindowsNative.OpenDirectory(path))
            : new StableDirectoryTree(path, UnixNative.OpenDirectoryAt(_handle, name));
    }

    public StableDirectoryTree CreateDirectory(string name)
    {
        ValidateName(name);
        var path = System.IO.Path.Combine(_path, name);
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
            Directory.CreateDirectory(path);
            return new StableDirectoryTree(path, WindowsNative.OpenDirectory(path));
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
        return new FileStream(handle, FileAccess.Write, 65_536, isAsync: false);
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
            using var current = parent.OpenDirectory(name);
            return OperatingSystem.IsWindows()
                ? WindowsNative.SameIdentity(_handle, current._handle)
                : UnixNative.SameIdentity(_handle, current._handle);
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void RenameChild(string sourceName, string destinationName)
    {
        ValidateName(sourceName);
        ValidateName(destinationName);
        if (OperatingSystem.IsWindows())
        {
            WindowsNative.EnsureSameDirectory(_handle, _path);
            Directory.Move(
                System.IO.Path.Combine(_path, sourceName),
                System.IO.Path.Combine(_path, destinationName));
            return;
        }

        UnixNative.RenameAt(_handle, sourceName, destinationName);
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
            expected.Dispose();
            Directory.Delete(System.IO.Path.Combine(_path, name));
        }
        else
        {
            UnixNative.UnlinkAt(_handle, name, UnixAtRemovedir);
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
                    child.Dispose();
                    Directory.Delete(System.IO.Path.Combine(_path, name));
                }
                else
                {
                    UnixNative.UnlinkAt(_handle, name, UnixAtRemovedir);
                }
            }
            catch (IOException)
            {
                if (OperatingSystem.IsWindows())
                {
                    File.Delete(System.IO.Path.Combine(_path, name));
                }
                else
                {
                    UnixNative.UnlinkAt(_handle, name, 0);
                }
            }
        }
    }

    public void Dispose() => _handle.Dispose();

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
        private const int WriteOnly = 1;
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

        public static SafeFileHandle CreateRegularFileAt(SafeFileHandle parent, string name) =>
            Wrap(openat(
                Fd(parent),
                name,
                WriteOnly | CreateFlag | ExclusiveFlag | NoFollowFlag | CloseOnExecFlag,
                FileMode));

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
            var duplicate = dup(Fd(directory));
            if (duplicate < 0)
            {
                throw Error("Could not duplicate a restore directory handle.");
            }

            var stream = fdopendir(duplicate);
            if (stream == IntPtr.Zero)
            {
                close(duplicate);
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

        public static void RenameAt(SafeFileHandle parent, string source, string destination)
        {
            if (renameat(Fd(parent), source, Fd(parent), destination) != 0)
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
        private const uint ShareRead = 1;
        private const uint ShareWrite = 2;
        private const uint ShareDelete = 4;
        private const uint OpenExisting = 3;
        private const uint CreateNew = 1;
        private const uint BackupSemantics = 0x02000000;
        private const uint OpenReparsePoint = 0x00200000;
        private const int FileAttributeTagInfo = 9;
        private const int FileIdInfo = 18;

        public static SafeFileHandle OpenDirectory(string path)
        {
            var handle = Open(path, GenericRead, OpenExisting, BackupSemantics | OpenReparsePoint);
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
            Open(path, GenericWrite, CreateNew, OpenReparsePoint);

        public static SafeFileHandle OpenRegularFile(string path)
        {
            var handle = Open(path, GenericRead, OpenExisting, OpenReparsePoint);
            EnsureNotReparse(handle);
            return handle;
        }

        public static void EnsureSameDirectory(SafeFileHandle expected, string path)
        {
            using var current = OpenDirectory(path);
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

        private static SafeFileHandle Open(string path, uint access, uint disposition, uint flags)
        {
            var handle = CreateFileW(
                path,
                access,
                ShareRead | ShareWrite | ShareDelete,
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
