using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Options;
using ShowVault.Api.HostedSync;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class S3HostedSyncIntegrationTests
{
    [Fact]
    public async Task Configured_s3_emulator_supports_conditional_chunk_commit_contract()
    {
        var endpoint = Environment.GetEnvironmentVariable("SHOWVAULT_S3_TEST_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint)) return;

        const string bucket = "showvault-prototype";
        var prefix = $"showvault/integration/{Guid.NewGuid():N}";
        using var client = new AmazonS3Client(new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1",
            RegionEndpoint = RegionEndpoint.USEast1,
            ForcePathStyle = true
        });
        var options = Options.Create(new HostedSyncOptions
        {
            Provider = HostedSyncProviders.S3,
            S3 = new S3HostedSyncOptions
            {
                Bucket = bucket,
                Region = "us-east-1",
                ServiceUrl = endpoint,
                Prefix = prefix,
                ForcePathStyle = true
            }
        });
        var objects = new S3HostedObjectStore(client, options);
        await objects.CheckAvailabilityAsync(CancellationToken.None);
        var store = new ObjectHostedSyncStore(objects, options, TimeProvider.System);
        var organizationId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes("real S3-compatible conditional bytes");
        var packageId = Digest("s3 integration package");
        var manifest = Manifest(packageId, content);

        await store.BeginAsync(
            organizationId, venueId, packageId, manifest, CancellationToken.None);
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "database V2", 0, content[..9],
            CancellationToken.None);
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "database V2", 0, content[..9],
            CancellationToken.None);
        Assert.Equal(9, await store.UploadedLengthAsync(
            organizationId, venueId, packageId, "database V2", CancellationToken.None));
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "database V2", 9, content[9..],
            CancellationToken.None);

        var receipt = await store.VerifyAndCommitAsync(
            organizationId, venueId, packageId, manifest, CancellationToken.None);
        var duplicate = await store.VerifyAndCommitAsync(
            organizationId, venueId, packageId, manifest, CancellationToken.None);
        Assert.Equal(receipt.CompletedAt, duplicate.CompletedAt);
        Assert.Equal(Digest(manifest), receipt.RemoteManifestSha256);

        var keys = await objects.ListAsync($"{prefix}/", CancellationToken.None);
        Assert.Single(keys, item => item.Key.EndsWith(
            "/receipt.json", StringComparison.Ordinal));
        Assert.All(keys, item => Assert.DoesNotContain(
            "database V2", item.Key, StringComparison.Ordinal));
    }

    private static byte[] Manifest(string packageId, byte[] content) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = "showvault.remote-package.v1",
            packageId,
            createdAt = DateTimeOffset.Parse("2026-08-10T20:00:00Z"),
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
                    relativePath = "database V2",
                    size = content.Length,
                    sha256 = Digest(content)
                }
            },
            localManifestSha256 = packageId
        });

    private static string Digest(string value) => Digest(Encoding.UTF8.GetBytes(value));

    private static string Digest(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}
