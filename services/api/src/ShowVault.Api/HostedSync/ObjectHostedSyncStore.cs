using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ShowVault.Api.HostedSync;

public sealed class ObjectHostedSyncStore(
    IHostedObjectStore objects,
    IOptions<HostedSyncOptions> options,
    TimeProvider timeProvider) : IHostedSyncStore
{
    private const int MaxChunkBytes = 256 * 1024;
    private const int MaxManifestBytes = 2 * 1024 * 1024;
    private const int MaxReceiptBytes = 64 * 1024;
    private readonly string _prefix = options.Value.S3.Prefix.Trim('/');

    public async Task<HostedSyncReceipt?> GetReceiptAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        CancellationToken cancellationToken)
    {
        ValidatePackageId(packageId);
        return await ReadReceiptAsync(
            ReceiptKey(organizationId, venueId, packageId),
            packageId,
            cancellationToken);
    }

    public async Task<HostedSyncReceipt?> BeginAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        byte[] manifestBytes,
        CancellationToken cancellationToken)
    {
        var manifest = HostedManifestValidator.Validate(packageId, manifestBytes);
        var receipt = await GetReceiptAsync(
            organizationId, venueId, packageId, cancellationToken);
        if (receipt is not null)
        {
            RequireManifestIdentity(receipt.RemoteManifestSha256, manifest.RemoteManifestSha256);
            return receipt;
        }

        var key = ManifestKey(organizationId, venueId, packageId);
        if (!await objects.PutIfAbsentAsync(key, manifestBytes, cancellationToken))
        {
            var existing = await RequireObjectAsync(key, MaxManifestBytes, cancellationToken);
            if (!existing.AsSpan().SequenceEqual(manifestBytes))
            {
                throw new HostedSyncConflictException(
                    "The partial package has a different manifest identity.");
            }
        }
        return null;
    }

    public async Task<long> UploadedLengthAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var manifest = await LoadManifestAsync(
            organizationId, venueId, packageId, cancellationToken);
        var descriptor = RequireDescriptor(manifest, relativePath);
        var receipt = await GetReceiptAsync(
            organizationId, venueId, packageId, cancellationToken);
        if (receipt is not null)
        {
            RequireManifestIdentity(receipt.RemoteManifestSha256, manifest.RemoteManifestSha256);
            return descriptor.Size;
        }
        var state = await LoadChunkStateAsync(
            organizationId, venueId, packageId, descriptor, cancellationToken);
        return state.Length;
    }

    public async Task AppendChunkAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        string relativePath,
        long offset,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        if (offset < 0 || bytes.Length is 0 or > MaxChunkBytes)
        {
            throw new HostedSyncValidationException("The hosted chunk range is invalid.");
        }
        var manifest = await LoadManifestAsync(
            organizationId, venueId, packageId, cancellationToken);
        var descriptor = RequireDescriptor(manifest, relativePath);
        if (offset > descriptor.Size || bytes.LongLength > descriptor.Size - offset)
        {
            throw new HostedSyncValidationException("The hosted chunk exceeds its file size.");
        }

        var state = await LoadChunkStateAsync(
            organizationId, venueId, packageId, descriptor, cancellationToken);
        if (offset < state.Length)
        {
            var existing = await ReadRangeAsync(state.Chunks, offset, bytes.Length,
                cancellationToken);
            if (existing.AsSpan().SequenceEqual(bytes)) return;
            throw new HostedSyncConflictException(
                "The hosted object offset is stale or conflicting.");
        }
        if (offset != state.Length)
        {
            throw new HostedSyncConflictException(
                "The hosted object offset is stale or conflicting.");
        }

        var key = ChunkKey(organizationId, venueId, packageId, relativePath, offset);
        if (!await objects.PutIfAbsentAsync(key, bytes, cancellationToken))
        {
            var existing = await RequireObjectAsync(key, MaxChunkBytes, cancellationToken);
            if (existing.AsSpan().SequenceEqual(bytes)) return;
            throw new HostedSyncConflictException(
                "The hosted object offset is stale or conflicting.");
        }
    }

    public async Task<HostedSyncReceipt> VerifyAndCommitAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        byte[] manifestBytes,
        CancellationToken cancellationToken)
    {
        var requested = HostedManifestValidator.Validate(packageId, manifestBytes);
        var existingReceipt = await GetReceiptAsync(
            organizationId, venueId, packageId, cancellationToken);
        if (existingReceipt is not null)
        {
            RequireManifestIdentity(
                existingReceipt.RemoteManifestSha256, requested.RemoteManifestSha256);
            return existingReceipt;
        }

        var stored = await LoadManifestAsync(
            organizationId, venueId, packageId, cancellationToken);
        RequireManifestIdentity(stored.RemoteManifestSha256, requested.RemoteManifestSha256);

        var expectedObjects = new HashSet<string>(StringComparer.Ordinal)
        {
            ManifestKey(organizationId, venueId, packageId)
        };
        foreach (var descriptor in stored.Files.Values)
        {
            var state = await LoadChunkStateAsync(
                organizationId, venueId, packageId, descriptor, cancellationToken);
            if (state.Length != descriptor.Size)
            {
                throw new HostedSyncConflictException(
                    "The hosted file set does not match the manifest.");
            }
            foreach (var chunk in state.Chunks) expectedObjects.Add(chunk.Key);
            if (await HashChunksAsync(state.Chunks, cancellationToken) != descriptor.Sha256)
            {
                throw new HostedSyncConflictException(
                    "Hosted checksum verification failed.");
            }
        }

        var receiptKey = ReceiptKey(organizationId, venueId, packageId);
        var actualObjects = await objects.ListAsync(
            $"{PackageRoot(organizationId, venueId, packageId)}/", cancellationToken);
        if (actualObjects.Any(item =>
                item.Key != receiptKey && !expectedObjects.Contains(item.Key)))
        {
            throw new HostedSyncConflictException(
                "The hosted package contains unsupported objects.");
        }

        var receipt = new HostedSyncReceipt(
            packageId, stored.RemoteManifestSha256, timeProvider.GetUtcNow());
        var receiptBytes = JsonSerializer.SerializeToUtf8Bytes(receipt);
        if (await objects.PutIfAbsentAsync(receiptKey, receiptBytes, cancellationToken))
        {
            return receipt;
        }
        var raced = await ReadReceiptAsync(receiptKey, packageId, cancellationToken)
            ?? throw new HostedSyncConflictException("The hosted receipt is missing.");
        RequireManifestIdentity(raced.RemoteManifestSha256, stored.RemoteManifestSha256);
        return raced;
    }

    private async Task<ValidatedHostedManifest> LoadManifestAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        CancellationToken cancellationToken)
    {
        ValidatePackageId(packageId);
        var bytes = await RequireObjectAsync(
            ManifestKey(organizationId, venueId, packageId),
            MaxManifestBytes,
            cancellationToken,
            "The hosted upload has not begun.");
        return HostedManifestValidator.Validate(packageId, bytes);
    }

    private async Task<_ChunkState> LoadChunkStateAsync(
        Guid organizationId,
        Guid venueId,
        string packageId,
        HostedSyncDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var prefix = ChunkPrefix(
            organizationId, venueId, packageId, descriptor.RelativePath);
        var objectsForFile = await objects.ListAsync(prefix, cancellationToken);
        var chunks = new List<_Chunk>(objectsForFile.Count);
        foreach (var item in objectsForFile)
        {
            if (!item.Key.StartsWith(prefix, StringComparison.Ordinal) ||
                item.Key.Length != prefix.Length + 26 ||
                !item.Key.EndsWith(".chunk", StringComparison.Ordinal) ||
                !long.TryParse(item.Key.AsSpan(prefix.Length, 20), out var offset) ||
                item.Size is <= 0 or > MaxChunkBytes)
            {
                throw new HostedSyncConflictException("Hosted chunk storage is malformed.");
            }
            chunks.Add(new _Chunk(item.Key, offset, checked((int)item.Size)));
        }
        chunks.Sort((left, right) => left.Offset.CompareTo(right.Offset));
        long length = 0;
        foreach (var chunk in chunks)
        {
            if (chunk.Offset != length || chunk.Size > descriptor.Size - length)
            {
                throw new HostedSyncConflictException(
                    "Hosted chunk storage is incomplete or conflicting.");
            }
            length += chunk.Size;
        }
        return new _ChunkState(chunks, length);
    }

    private async Task<byte[]> ReadRangeAsync(
        IReadOnlyList<_Chunk> chunks,
        long offset,
        int count,
        CancellationToken cancellationToken)
    {
        var result = new byte[count];
        var written = 0;
        var position = offset;
        foreach (var chunk in chunks)
        {
            var chunkEnd = chunk.Offset + chunk.Size;
            if (position >= chunkEnd) continue;
            if (position < chunk.Offset) break;
            var bytes = await RequireObjectAsync(chunk.Key, MaxChunkBytes, cancellationToken);
            if (bytes.Length != chunk.Size)
            {
                throw new HostedSyncConflictException("A hosted chunk changed size.");
            }
            var sourceOffset = checked((int)(position - chunk.Offset));
            var copied = Math.Min(bytes.Length - sourceOffset, count - written);
            bytes.AsSpan(sourceOffset, copied).CopyTo(result.AsSpan(written));
            written += copied;
            position += copied;
            if (written == count) return result;
        }
        throw new HostedSyncConflictException("A hosted chunk range is incomplete.");
    }

    private async Task<string> HashChunksAsync(
        IReadOnlyList<_Chunk> chunks,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var chunk in chunks)
        {
            var bytes = await RequireObjectAsync(chunk.Key, MaxChunkBytes, cancellationToken);
            if (bytes.Length != chunk.Size)
            {
                throw new HostedSyncConflictException("A hosted chunk changed size.");
            }
            hash.AppendData(bytes);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private async Task<HostedSyncReceipt?> ReadReceiptAsync(
        string key,
        string packageId,
        CancellationToken cancellationToken)
    {
        var bytes = await objects.ReadAsync(key, MaxReceiptBytes, cancellationToken);
        if (bytes is null) return null;
        try
        {
            var receipt = JsonSerializer.Deserialize<HostedSyncReceipt>(bytes);
            if (receipt is null || receipt.PackageId != packageId ||
                !IsSha256(receipt.RemoteManifestSha256))
            {
                throw new HostedSyncConflictException(
                    "The hosted receipt identity is invalid.");
            }
            return receipt;
        }
        catch (JsonException)
        {
            throw new HostedSyncConflictException("The hosted receipt is malformed.");
        }
    }

    private async Task<byte[]> RequireObjectAsync(
        string key,
        int maximumBytes,
        CancellationToken cancellationToken,
        string missingMessage = "A hosted object is missing.") =>
        await objects.ReadAsync(key, maximumBytes, cancellationToken)
        ?? throw new HostedSyncConflictException(missingMessage);

    private static HostedSyncDescriptor RequireDescriptor(
        ValidatedHostedManifest manifest,
        string relativePath)
    {
        HostedManifestValidator.ValidateLogicalPath(relativePath);
        if (!manifest.Files.TryGetValue(relativePath, out var descriptor))
        {
            throw new HostedSyncValidationException(
                "The hosted file is not in the manifest.");
        }
        return descriptor;
    }

    private string PackageRoot(Guid organizationId, Guid venueId, string packageId) =>
        $"{_prefix}/{organizationId:N}/{venueId:N}/packages/{packageId}";

    private string ManifestKey(Guid organizationId, Guid venueId, string packageId) =>
        $"{PackageRoot(organizationId, venueId, packageId)}/manifest.json";

    private string ReceiptKey(Guid organizationId, Guid venueId, string packageId) =>
        $"{PackageRoot(organizationId, venueId, packageId)}/receipt.json";

    private string ChunkPrefix(
        Guid organizationId,
        Guid venueId,
        string packageId,
        string relativePath) =>
        $"{PackageRoot(organizationId, venueId, packageId)}/files/" +
        $"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(relativePath)))}/chunks/";

    private string ChunkKey(
        Guid organizationId,
        Guid venueId,
        string packageId,
        string relativePath,
        long offset) =>
        $"{ChunkPrefix(organizationId, venueId, packageId, relativePath)}{offset:D20}.chunk";

    private static void RequireManifestIdentity(string actual, string expected)
    {
        if (actual != expected)
        {
            throw new HostedSyncConflictException(
                "The hosted package has a different manifest identity.");
        }
    }

    private static void ValidatePackageId(string packageId)
    {
        if (!IsSha256(packageId))
        {
            throw new HostedSyncValidationException("The hosted package ID is invalid.");
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record _Chunk(string Key, long Offset, int Size);
    private sealed record _ChunkState(IReadOnlyList<_Chunk> Chunks, long Length);
}
