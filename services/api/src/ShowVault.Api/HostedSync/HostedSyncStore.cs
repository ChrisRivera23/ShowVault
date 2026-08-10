using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ShowVault.Api.HostedSync;

public sealed class HostedSyncStore(IOptions<HostedSyncOptions> options, TimeProvider timeProvider)
    : IHostedSyncStore
{
    private const int MaxChunkBytes = 256 * 1024;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();
    private readonly string? _root = string.IsNullOrWhiteSpace(options.Value.RootPath)
        ? null
        : Path.GetFullPath(options.Value.RootPath);

    public async Task<HostedSyncReceipt?> GetReceiptAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        CancellationToken cancellationToken)
    {
        ValidatePackageId(packageId);
        return await WithLockAsync(organizationId, venueId, packageId, async () =>
        {
            var receiptPath = Path.Combine(CommittedRoot(organizationId, venueId, packageId),
                "receipt.json");
            return await ReadReceiptAsync(receiptPath, packageId, cancellationToken);
        }, cancellationToken);
    }

    public async Task<HostedSyncReceipt?> BeginAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        byte[] manifestBytes,
        CancellationToken cancellationToken)
    {
        var manifest = HostedManifestValidator.Validate(packageId, manifestBytes);
        return await WithLockAsync(organizationId, venueId, packageId, async () =>
        {
            var existing = await ReadReceiptAsync(
                Path.Combine(CommittedRoot(organizationId, venueId, packageId), "receipt.json"),
                packageId,
                cancellationToken);
            if (existing is not null)
            {
                if (existing.RemoteManifestSha256 != manifest.RemoteManifestSha256)
                {
                    throw new HostedSyncConflictException(
                        "The committed package has a different manifest identity.");
                }
                return existing;
            }

            var partial = PartialRoot(organizationId, venueId, packageId);
            EnsureSafeDirectory(partial);
            var manifestPath = Path.Combine(partial, "manifest.json");
            if (File.Exists(manifestPath))
            {
                RejectLink(manifestPath);
                var existingBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
                if (!existingBytes.AsSpan().SequenceEqual(manifestBytes))
                {
                    throw new HostedSyncConflictException(
                        "The partial package has a different manifest identity.");
                }
            }
            else
            {
                await WriteNewFileAsync(manifestPath, manifestBytes, cancellationToken);
            }
            EnsureSafeDirectory(Path.Combine(partial, "content"));
            return null;
        }, cancellationToken);
    }

    public Task<long> UploadedLengthAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        string relativePath,
        CancellationToken cancellationToken) =>
        WithLockAsync(organizationId, venueId, packageId, async () =>
        {
            var committed = await ReadReceiptAsync(
                Path.Combine(CommittedRoot(organizationId, venueId, packageId), "receipt.json"),
                packageId,
                cancellationToken);
            if (committed is not null)
            {
                var committedManifest = await LoadCommittedManifestAsync(
                    organizationId, venueId, packageId, cancellationToken);
                return RequireDescriptor(committedManifest, relativePath).Size;
            }
            var manifest = await LoadPartialManifestAsync(
                organizationId, venueId, packageId, cancellationToken);
            var descriptor = RequireDescriptor(manifest, relativePath);
            var file = ContentPath(organizationId, venueId, packageId, relativePath);
            if (!File.Exists(file))
            {
                if (descriptor.Size == 0)
                {
                    EnsureSafeDirectory(Path.GetDirectoryName(file)!);
                    await WriteNewFileAsync(file, [], cancellationToken);
                }
                return 0;
            }
            RejectLink(file);
            var length = new FileInfo(file).Length;
            if (length > descriptor.Size)
            {
                throw new HostedSyncConflictException("A hosted object exceeds its manifest size.");
            }
            return length;
        }, cancellationToken);

    public Task AppendChunkAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        string relativePath,
        long offset,
        byte[] bytes,
        CancellationToken cancellationToken) =>
        WithLockAsync(organizationId, venueId, packageId, async () =>
        {
            if (offset < 0 || bytes.Length is 0 or > MaxChunkBytes)
            {
                throw new HostedSyncValidationException("The hosted chunk range is invalid.");
            }
            var committed = await ReadReceiptAsync(
                Path.Combine(CommittedRoot(organizationId, venueId, packageId), "receipt.json"),
                packageId,
                cancellationToken);
            if (committed is not null)
            {
                var committedManifest = await LoadCommittedManifestAsync(
                    organizationId, venueId, packageId, cancellationToken);
                var committedDescriptor = RequireDescriptor(committedManifest, relativePath);
                if (offset > committedDescriptor.Size ||
                    bytes.LongLength > committedDescriptor.Size - offset)
                {
                    throw new HostedSyncConflictException(
                        "The committed hosted chunk range conflicts with the package.");
                }
                var committedFile = CommittedContentPath(
                    organizationId, venueId, packageId, relativePath);
                await using var committedStream = new FileStream(
                    committedFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                    64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                committedStream.Position = offset;
                var existingBytes = new byte[bytes.Length];
                await committedStream.ReadExactlyAsync(existingBytes, cancellationToken);
                if (existingBytes.AsSpan().SequenceEqual(bytes)) return;
                throw new HostedSyncConflictException(
                    "The committed hosted chunk bytes conflict with the package.");
            }
            var manifest = await LoadPartialManifestAsync(
                organizationId, venueId, packageId, cancellationToken);
            var descriptor = RequireDescriptor(manifest, relativePath);
            if (offset > descriptor.Size || bytes.LongLength > descriptor.Size - offset)
            {
                throw new HostedSyncValidationException("The hosted chunk exceeds its file size.");
            }
            var file = ContentPath(organizationId, venueId, packageId, relativePath);
            EnsureSafeDirectory(Path.GetDirectoryName(file)!);
            if (File.Exists(file)) RejectLink(file);
            await using var stream = new FileStream(
                file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
            if (stream.Length == offset)
            {
                stream.Position = offset;
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return;
            }
            if (stream.Length >= offset + bytes.LongLength)
            {
                var existing = new byte[bytes.Length];
                stream.Position = offset;
                await stream.ReadExactlyAsync(existing, cancellationToken);
                if (existing.AsSpan().SequenceEqual(bytes)) return;
            }
            throw new HostedSyncConflictException("The hosted object offset is stale or conflicting.");
        }, cancellationToken);

    public Task<HostedSyncReceipt> VerifyAndCommitAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        byte[] manifestBytes,
        CancellationToken cancellationToken) =>
        WithLockAsync(organizationId, venueId, packageId, async () =>
        {
            var requested = HostedManifestValidator.Validate(packageId, manifestBytes);
            var receiptPath = Path.Combine(CommittedRoot(organizationId, venueId, packageId),
                "receipt.json");
            var existing = await ReadReceiptAsync(receiptPath, packageId, cancellationToken);
            if (existing is not null)
            {
                if (existing.RemoteManifestSha256 != requested.RemoteManifestSha256)
                {
                    throw new HostedSyncConflictException(
                        "The committed package has a different manifest identity.");
                }
                return existing;
            }

            var stored = await LoadPartialManifestAsync(
                organizationId, venueId, packageId, cancellationToken);
            if (stored.RemoteManifestSha256 != requested.RemoteManifestSha256)
            {
                throw new HostedSyncConflictException(
                    "The commit manifest does not match the begun upload.");
            }
            var contentRoot = Path.Combine(PartialRoot(organizationId, venueId, packageId), "content");
            var actualFiles = EnumerateSafeFiles(contentRoot)
                .Select(path => Path.GetRelativePath(contentRoot, path)
                    .Replace(Path.DirectorySeparatorChar, '/'))
                .ToHashSet(StringComparer.Ordinal);
            if (actualFiles.Count != stored.Files.Count ||
                stored.Files.Keys.Any(path => !actualFiles.Contains(path)))
            {
                throw new HostedSyncConflictException(
                    "The hosted file set does not match the manifest.");
            }
            foreach (var descriptor in stored.Files.Values)
            {
                var file = ContentPath(organizationId, venueId, packageId, descriptor.RelativePath);
                if (new FileInfo(file).Length != descriptor.Size ||
                    await HashFileAsync(file, cancellationToken) != descriptor.Sha256)
                {
                    throw new HostedSyncConflictException(
                        "Hosted checksum verification failed.");
                }
            }

            var receipt = new HostedSyncReceipt(
                packageId, stored.RemoteManifestSha256, timeProvider.GetUtcNow());
            var partial = PartialRoot(organizationId, venueId, packageId);
            await WriteNewFileAsync(
                Path.Combine(partial, "receipt.json"),
                JsonSerializer.SerializeToUtf8Bytes(receipt),
                cancellationToken);
            var committed = CommittedRoot(organizationId, venueId, packageId);
            EnsureSafeDirectory(Path.GetDirectoryName(committed)!);
            Directory.Move(partial, committed);
            return receipt;
        }, cancellationToken);

    private async Task<ValidatedHostedManifest> LoadPartialManifestAsync(
        Guid organizationId, Guid venueId, string packageId, CancellationToken cancellationToken)
    {
        ValidatePackageId(packageId);
        var path = Path.Combine(PartialRoot(organizationId, venueId, packageId), "manifest.json");
        if (!File.Exists(path))
        {
            throw new HostedSyncConflictException("The hosted upload has not begun.");
        }
        RejectLink(path);
        var info = new FileInfo(path);
        if (info.Length > 2 * 1024 * 1024)
        {
            throw new HostedSyncConflictException("The stored hosted manifest is oversized.");
        }
        return HostedManifestValidator.Validate(
            packageId, await File.ReadAllBytesAsync(path, cancellationToken));
    }

    private async Task<ValidatedHostedManifest> LoadCommittedManifestAsync(
        Guid organizationId, Guid venueId, string packageId,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(CommittedRoot(organizationId, venueId, packageId), "manifest.json");
        if (!File.Exists(path))
            throw new HostedSyncConflictException("Committed hosted manifest is missing.");
        RejectLink(path);
        if (new FileInfo(path).Length > 2 * 1024 * 1024)
            throw new HostedSyncConflictException("Committed hosted manifest is oversized.");
        return HostedManifestValidator.Validate(
            packageId, await File.ReadAllBytesAsync(path, cancellationToken));
    }

    private static HostedSyncDescriptor RequireDescriptor(
        ValidatedHostedManifest manifest, string relativePath)
    {
        HostedManifestValidator.ValidateLogicalPath(relativePath);
        if (!manifest.Files.TryGetValue(relativePath, out var descriptor))
        {
            throw new HostedSyncValidationException("The hosted file is not in the manifest.");
        }
        return descriptor;
    }

    private string ContentPath(
        Guid organizationId, Guid venueId, string packageId, string relativePath)
    {
        var path = Path.Combine(PartialRoot(organizationId, venueId, packageId), "content");
        foreach (var segment in HostedManifestValidator.ValidateLogicalPath(relativePath))
        {
            path = Path.Combine(path, segment);
        }
        return path;
    }

    private string CommittedContentPath(
        Guid organizationId, Guid venueId, string packageId, string relativePath)
    {
        var path = Path.Combine(CommittedRoot(organizationId, venueId, packageId), "content");
        foreach (var segment in HostedManifestValidator.ValidateLogicalPath(relativePath))
            path = Path.Combine(path, segment);
        return path;
    }

    private string PartialRoot(Guid organizationId, Guid venueId, string packageId) =>
        Path.Combine(TenantRoot(organizationId, venueId), ".partial", packageId);

    private string CommittedRoot(Guid organizationId, Guid venueId, string packageId) =>
        Path.Combine(TenantRoot(organizationId, venueId), "packages", packageId);

    private string TenantRoot(Guid organizationId, Guid venueId)
    {
        var root = _root ?? throw new HostedSyncUnavailableException(
            "Hosted synchronization storage is not configured.");
        EnsureSafeDirectory(root);
        return Path.Combine(root, organizationId.ToString("N"), venueId.ToString("N"));
    }

    private static IEnumerable<string> EnumerateSafeFiles(string root)
    {
        if (!Directory.Exists(root))
        {
            throw new HostedSyncConflictException("Hosted package content is missing.");
        }
        foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            RejectLink(path);
            if (Directory.Exists(path)) continue;
            if (!File.Exists(path))
            {
                throw new HostedSyncConflictException("Hosted package content is unsafe.");
            }
            yield return path;
        }
    }

    private void EnsureSafeDirectory(string path)
    {
        var root = _root ?? throw new HostedSyncUnavailableException(
            "Hosted synchronization storage is not configured.");
        Directory.CreateDirectory(root);
        RejectLink(root);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}"))
        {
            throw new HostedSyncConflictException("Hosted storage escaped its configured root.");
        }
        var current = root;
        if (relative == ".") return;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            Directory.CreateDirectory(current);
            RejectLink(current);
        }
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new HostedSyncConflictException("Hosted storage contains a filesystem link.");
        }
    }

    private async Task WriteNewFileAsync(
        string path, byte[] bytes, CancellationToken cancellationToken)
    {
        EnsureSafeDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task<HostedSyncReceipt?> ReadReceiptAsync(
        string path, string packageId, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        RejectLink(path);
        if (new FileInfo(path).Length > 64 * 1024)
        {
            throw new HostedSyncConflictException("The hosted receipt is oversized.");
        }
        try
        {
            var receipt = JsonSerializer.Deserialize<HostedSyncReceipt>(
                await File.ReadAllBytesAsync(path, cancellationToken));
            if (receipt is null || receipt.PackageId != packageId ||
                !System.Text.RegularExpressions.Regex.IsMatch(
                    receipt.RemoteManifestSha256, "^[0-9a-f]{64}$"))
            {
                throw new HostedSyncConflictException("The hosted receipt identity is invalid.");
            }
            return receipt;
        }
        catch (JsonException)
        {
            throw new HostedSyncConflictException("The hosted receipt is malformed.");
        }
    }

    private static async Task<string> HashFileAsync(
        string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void ValidatePackageId(string packageId)
    {
        if (packageId.Length != 64 || packageId.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new HostedSyncValidationException("The hosted package ID is invalid.");
        }
    }

    private static async Task<T> WithLockAsync<T>(
        Guid organizationId, Guid venueId, string packageId,
        Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var key = $"{organizationId:N}/{venueId:N}/{packageId}";
        var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { gate.Release(); }
    }

    private static Task WithLockAsync(
        Guid organizationId, Guid venueId, string packageId,
        Func<Task> action, CancellationToken cancellationToken) =>
        WithLockAsync(organizationId, venueId, packageId, async () =>
        {
            await action();
            return true;
        }, cancellationToken);
}
