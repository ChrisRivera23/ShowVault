using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShowVault.Api.HostedSync;

public static class HostedSyncSmokeCheck
{
    public static async Task RunAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var store = services.GetRequiredService<IHostedSyncStore>();
        var organizationId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes("ShowVault deployable object-storage smoke check");
        var packageId = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes($"showvault-smoke-{Guid.NewGuid():N}")));
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = "showvault.remote-package.v1",
            packageId,
            createdAt = DateTimeOffset.UtcNow,
            source = new
            {
                candidateKey = "macos.serato-dj-pro.user-data",
                pluginId = "showvault.serato-dj-pro",
                productName = "Serato DJ Pro"
            },
            files = new[]
            {
                new
                {
                    relativePath = "smoke-check.bin",
                    size = content.Length,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(content))
                }
            },
            localManifestSha256 = packageId
        });

        await store.BeginAsync(
            organizationId, venueId, packageId, manifest, cancellationToken);
        var first = content[..8];
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "smoke-check.bin", 0, first,
            cancellationToken);
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "smoke-check.bin", 0, first,
            cancellationToken);
        if (await store.UploadedLengthAsync(
                organizationId, venueId, packageId, "smoke-check.bin",
                cancellationToken) != first.Length)
        {
            throw new InvalidOperationException(
                "Hosted synchronization did not preserve the resumable offset.");
        }
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "smoke-check.bin", first.Length,
            content[first.Length..], cancellationToken);
        var receipt = await store.VerifyAndCommitAsync(
            organizationId, venueId, packageId, manifest, cancellationToken);
        var duplicate = await store.VerifyAndCommitAsync(
            organizationId, venueId, packageId, manifest, cancellationToken);
        if (receipt.CompletedAt != duplicate.CompletedAt ||
            receipt.RemoteManifestSha256 !=
            Convert.ToHexStringLower(SHA256.HashData(manifest)))
        {
            throw new InvalidOperationException(
                "Hosted synchronization completion was not idempotent.");
        }
        Console.WriteLine($"Hosted synchronization smoke check passed: {packageId}");
    }
}
