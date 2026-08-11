using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ShowVault.Agent.Identity;

[SupportedOSPlatform("macos")]
public sealed partial class MacOsKeychainCredentialStore : IAgentCredentialStore
{
    private const int Success = 0;
    private const int ItemNotFound = -25300;
    private static readonly byte[] Service = Encoding.UTF8.GetBytes("com.showvault.venue-agent");
    private static readonly byte[] Account = Encoding.UTF8.GetBytes("identity");

    public ValueTask<StoredAgentState?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)Service.Length,
            Service,
            (uint)Account.Length,
            Account,
            out var passwordLength,
            out var passwordData,
            out var item);
        if (status == ItemNotFound)
        {
            return ValueTask.FromResult<StoredAgentState?>(null);
        }

        ThrowIfFailed(status, "read");
        try
        {
            var bytes = new byte[passwordLength];
            Marshal.Copy(passwordData, bytes, 0, bytes.Length);
            return ValueTask.FromResult<StoredAgentState?>(
                AgentCredentialSerialization.Deserialize(Encoding.UTF8.GetString(bytes)));
        }
        finally
        {
            _ = SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            if (item != IntPtr.Zero)
            {
                CFRelease(item);
            }
        }
    }

    public ValueTask SaveAsync(
        StoredAgentState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var secret = Encoding.UTF8.GetBytes(AgentCredentialSerialization.Serialize(state));
        var findStatus = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)Service.Length,
            Service,
            (uint)Account.Length,
            Account,
            out _,
            out var existingData,
            out var existingItem);

        if (findStatus == ItemNotFound)
        {
            var addStatus = SecKeychainAddGenericPassword(
                IntPtr.Zero,
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
            var updateStatus = SecKeychainItemModifyAttributesAndData(
                existingItem,
                IntPtr.Zero,
                (uint)secret.Length,
                secret);
            ThrowIfFailed(updateStatus, "update");
            return ValueTask.CompletedTask;
        }
        finally
        {
            _ = SecKeychainItemFreeContent(IntPtr.Zero, existingData);
            if (existingItem != IntPtr.Zero)
            {
                CFRelease(existingItem);
            }
        }
    }

    public ValueTask DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)Service.Length,
            Service,
            (uint)Account.Length,
            Account,
            out _,
            out var existingData,
            out var item);
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
            _ = SecKeychainItemFreeContent(IntPtr.Zero, existingData);
            if (item != IntPtr.Zero)
            {
                CFRelease(item);
            }
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

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(IntPtr value);
}
