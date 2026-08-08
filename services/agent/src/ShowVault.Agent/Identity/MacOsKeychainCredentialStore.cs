using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Identity;

[SupportedOSPlatform("macos")]
public sealed partial class MacOsKeychainCredentialStore(IOptions<AgentOptions> options) : IAgentCredentialStore
{
    private const int Success = 0;
    private const int ItemNotFound = -25300;
    private static readonly byte[] Service = Encoding.UTF8.GetBytes("com.showvault.venue-agent");
    private static readonly byte[] Account = Encoding.UTF8.GetBytes("identity");

    public ValueTask<StoredAgentIdentity?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keychain = OpenConfiguredKeychain();
        try
        {
            var status = Find(keychain, out var passwordLength, out var passwordData, out var item);
            if (status == ItemNotFound)
            {
                return ValueTask.FromResult<StoredAgentIdentity?>(null);
            }

            ThrowIfFailed(status, "read");
            try
            {
                var bytes = new byte[passwordLength];
                Marshal.Copy(passwordData, bytes, 0, bytes.Length);
                return ValueTask.FromResult<StoredAgentIdentity?>(
                    AgentCredentialSerialization.Deserialize(Encoding.UTF8.GetString(bytes)));
            }
            finally
            {
                FreeItem(passwordData, item);
            }
        }
        finally
        {
            ReleaseConfiguredKeychain(keychain);
        }
    }

    public ValueTask SaveAsync(StoredAgentIdentity identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var secret = Encoding.UTF8.GetBytes(AgentCredentialSerialization.Serialize(identity));
        var keychain = OpenConfiguredKeychain();
        try
        {
            var findStatus = Find(keychain, out _, out var existingData, out var existingItem);
            if (findStatus == ItemNotFound)
            {
                var addStatus = SecKeychainAddGenericPassword(
                    keychain,
                    (uint)Service.Length,
                    Service,
                    (uint)Account.Length,
                    Account,
                    (uint)secret.Length,
                    secret,
                    out var addedItem);
                if (addedItem != IntPtr.Zero)
                {
                    CFRelease(addedItem);
                }

                ThrowIfFailed(addStatus, "write");
                return ValueTask.CompletedTask;
            }

            ThrowIfFailed(findStatus, "find");
            try
            {
                ThrowIfFailed(
                    SecKeychainItemModifyAttributesAndData(
                        existingItem,
                        IntPtr.Zero,
                        (uint)secret.Length,
                        secret),
                    "update");
                return ValueTask.CompletedTask;
            }
            finally
            {
                FreeItem(existingData, existingItem);
            }
        }
        finally
        {
            ReleaseConfiguredKeychain(keychain);
        }
    }

    public ValueTask DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keychain = OpenConfiguredKeychain();
        try
        {
            var status = Find(keychain, out _, out var existingData, out var item);
            if (status == ItemNotFound)
            {
                return ValueTask.CompletedTask;
            }

            ThrowIfFailed(status, "find");
            try
            {
                ThrowIfFailed(SecKeychainItemDelete(item), "delete");
                return ValueTask.CompletedTask;
            }
            finally
            {
                FreeItem(existingData, item);
            }
        }
        finally
        {
            ReleaseConfiguredKeychain(keychain);
        }
    }

    private static int Find(
        IntPtr keychain,
        out uint passwordLength,
        out IntPtr passwordData,
        out IntPtr item) => SecKeychainFindGenericPassword(
            keychain,
            (uint)Service.Length,
            Service,
            (uint)Account.Length,
            Account,
            out passwordLength,
            out passwordData,
            out item);

    private IntPtr OpenConfiguredKeychain()
    {
        if (string.IsNullOrWhiteSpace(options.Value.MacOsKeychainPath))
        {
            return IntPtr.Zero;
        }

        ThrowIfFailed(
            SecKeychainOpen(options.Value.MacOsKeychainPath, out var keychain),
            "open dedicated keychain");
        try
        {
            var password = File.ReadAllText(options.Value.MacOsKeychainPasswordFile!).TrimEnd('\r', '\n');
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            ThrowIfFailed(
                SecKeychainUnlock(keychain, (uint)passwordBytes.Length, passwordBytes, true),
                "unlock dedicated keychain");
            return keychain;
        }
        catch
        {
            CFRelease(keychain);
            throw;
        }
    }

    private static void ReleaseConfiguredKeychain(IntPtr keychain)
    {
        if (keychain != IntPtr.Zero)
        {
            CFRelease(keychain);
        }
    }

    private static void FreeItem(IntPtr data, IntPtr item)
    {
        if (data != IntPtr.Zero)
        {
            _ = SecKeychainItemFreeContent(IntPtr.Zero, data);
        }

        if (item != IntPtr.Zero)
        {
            CFRelease(item);
        }
    }

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status != Success)
        {
            throw new InvalidOperationException(
                $"macOS Keychain operation '{operation}' failed with status {status}.");
        }
    }

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecKeychainFindGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        out uint passwordLength,
        out IntPtr passwordData,
        out IntPtr itemRef);

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecKeychainAddGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        uint passwordLength,
        byte[] passwordData,
        out IntPtr itemRef);

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecKeychainItemModifyAttributesAndData(
        IntPtr itemRef,
        IntPtr attributeList,
        uint length,
        byte[] data);

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecKeychainItemFreeContent(IntPtr attributeList, IntPtr data);

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecKeychainItemDelete(IntPtr itemRef);

    [LibraryImport(
        "/System/Library/Frameworks/Security.framework/Security",
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial int SecKeychainOpen(string pathName, out IntPtr keychain);

    [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
    private static partial int SecKeychainUnlock(
        IntPtr keychain,
        uint passwordLength,
        byte[] password,
        [MarshalAs(UnmanagedType.Bool)] bool usePassword);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(IntPtr value);
}
