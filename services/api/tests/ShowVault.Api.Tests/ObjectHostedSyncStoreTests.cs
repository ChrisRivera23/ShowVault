using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ShowVault.Api.HostedSync;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class ObjectHostedSyncStoreTests
{
    [Fact]
    public async Task Immutable_chunks_resume_verify_and_publish_one_receipt()
    {
        var objects = new InMemoryHostedObjectStore();
        var store = CreateStore(objects);
        var organizationId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes("synthetic object storage bytes");
        var packageId = Digest("object package");
        var manifest = Manifest(packageId, content);

        Assert.Null(await store.BeginAsync(
            organizationId, venueId, packageId, manifest, CancellationToken.None));
        Assert.Equal(0, await store.UploadedLengthAsync(
            organizationId, venueId, packageId, "database V2",
            CancellationToken.None));

        var first = content[..8];
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "database V2", 0, first,
            CancellationToken.None);
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "database V2", 0, first,
            CancellationToken.None);
        Assert.Equal(8, await store.UploadedLengthAsync(
            organizationId, venueId, packageId, "database V2",
            CancellationToken.None));

        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "database V2", 8, content[8..],
            CancellationToken.None);
        var receipt = await store.VerifyAndCommitAsync(
            organizationId, venueId, packageId, manifest,
            CancellationToken.None);
        var duplicate = await store.VerifyAndCommitAsync(
            organizationId, venueId, packageId, manifest,
            CancellationToken.None);

        Assert.Equal(packageId, receipt.PackageId);
        Assert.Equal(Digest(manifest), receipt.RemoteManifestSha256);
        Assert.Equal(receipt.CompletedAt, duplicate.CompletedAt);
        Assert.Equal(content.Length, await store.UploadedLengthAsync(
            organizationId, venueId, packageId, "database V2",
            CancellationToken.None));
        Assert.Single(objects.Keys, key => key.EndsWith(
            "/receipt.json", StringComparison.Ordinal));
        Assert.All(objects.Keys, key =>
        {
            Assert.DoesNotContain("database V2", key, StringComparison.Ordinal);
            Assert.DoesNotContain("/private/", key, StringComparison.Ordinal);
            Assert.StartsWith(
                $"showvault/v1/{organizationId:N}/{venueId:N}/packages/{packageId}/",
                key,
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Conflicting_duplicate_and_incomplete_commit_are_rejected()
    {
        var objects = new InMemoryHostedObjectStore();
        var store = CreateStore(objects);
        var organizationId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes("expected object bytes");
        var packageId = Digest("conflict package");
        var manifest = Manifest(packageId, content);
        await store.BeginAsync(
            organizationId, venueId, packageId, manifest,
            CancellationToken.None);
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "database V2", 0, content[..4],
            CancellationToken.None);

        await Assert.ThrowsAsync<HostedSyncConflictException>(() =>
            store.AppendChunkAsync(
                organizationId, venueId, packageId, "database V2", 0,
                Encoding.UTF8.GetBytes("nope"), CancellationToken.None));
        await Assert.ThrowsAsync<HostedSyncConflictException>(() =>
            store.VerifyAndCommitAsync(
                organizationId, venueId, packageId, manifest,
                CancellationToken.None));
        Assert.DoesNotContain(objects.Keys, key => key.EndsWith(
            "/receipt.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tampered_or_extra_objects_never_publish_completion()
    {
        var objects = new InMemoryHostedObjectStore();
        var store = CreateStore(objects);
        var organizationId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes("expected");
        var packageId = Digest("tampered object package");
        var manifest = Manifest(packageId, content);
        await store.BeginAsync(
            organizationId, venueId, packageId, manifest,
            CancellationToken.None);
        await store.AppendChunkAsync(
            organizationId, venueId, packageId, "database V2", 0, content,
            CancellationToken.None);
        objects.Replace(
            objects.Keys.Single(key => key.EndsWith(".chunk", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes("tampered"));

        await Assert.ThrowsAsync<HostedSyncConflictException>(() =>
            store.VerifyAndCommitAsync(
                organizationId, venueId, packageId, manifest,
                CancellationToken.None));
        Assert.DoesNotContain(objects.Keys, key => key.EndsWith(
            "/receipt.json", StringComparison.Ordinal));

        objects.Replace(
            objects.Keys.Single(key => key.EndsWith(".chunk", StringComparison.Ordinal)),
            content);
        objects.Replace(
            $"showvault/v1/{organizationId:N}/{venueId:N}/packages/{packageId}/unexpected",
            [1]);
        await Assert.ThrowsAsync<HostedSyncConflictException>(() =>
            store.VerifyAndCommitAsync(
                organizationId, venueId, packageId, manifest,
                CancellationToken.None));
    }

    [Fact]
    public async Task Tenants_are_isolated_and_concurrent_commit_is_idempotent()
    {
        var objects = new InMemoryHostedObjectStore();
        var store = CreateStore(objects);
        var firstOrganization = Guid.NewGuid();
        var secondOrganization = Guid.NewGuid();
        var firstVenue = Guid.NewGuid();
        var secondVenue = Guid.NewGuid();
        var packageId = Digest("shared object package");
        var content = Encoding.UTF8.GetBytes("tenant one");
        var manifest = Manifest(packageId, content);
        await store.BeginAsync(
            firstOrganization, firstVenue, packageId, manifest,
            CancellationToken.None);
        await store.AppendChunkAsync(
            firstOrganization, firstVenue, packageId, "database V2", 0, content,
            CancellationToken.None);

        var commits = await Task.WhenAll(
            store.VerifyAndCommitAsync(
                firstOrganization, firstVenue, packageId, manifest,
                CancellationToken.None),
            store.VerifyAndCommitAsync(
                firstOrganization, firstVenue, packageId, manifest,
                CancellationToken.None));
        Assert.Equal(commits[0].CompletedAt, commits[1].CompletedAt);
        Assert.Null(await store.GetReceiptAsync(
            secondOrganization, secondVenue, packageId,
            CancellationToken.None));
    }

    [Fact]
    public async Task Unavailable_object_transport_is_retryable()
    {
        var objects = new InMemoryHostedObjectStore { Available = false };
        var store = CreateStore(objects);
        await Assert.ThrowsAsync<HostedSyncUnavailableException>(() =>
            store.GetReceiptAsync(
                Guid.NewGuid(), Guid.NewGuid(), Digest("unavailable"),
                CancellationToken.None));
    }

    [Fact]
    public void Production_configuration_fails_closed()
    {
        var production = new TestHostEnvironment(Environments.Production);
        var validator = new HostedSyncOptionsValidator(production);
        Assert.True(validator.Validate(null, new HostedSyncOptions()).Failed);
        Assert.True(validator.Validate(null, new HostedSyncOptions
        {
            Provider = HostedSyncProviders.FileSystem,
            RootPath = "/tmp/showvault"
        }).Failed);
        Assert.True(validator.Validate(null, new HostedSyncOptions
        {
            Provider = HostedSyncProviders.S3,
            S3 = new S3HostedSyncOptions
            {
                Bucket = "showvault-prototype",
                Region = "us-east-1",
                Prefix = "showvault/v1"
            }
        }).Succeeded);
    }

    private static ObjectHostedSyncStore CreateStore(InMemoryHostedObjectStore objects) =>
        new(objects, Options.Create(new HostedSyncOptions
        {
            Provider = HostedSyncProviders.S3,
            S3 = new S3HostedSyncOptions
            {
                Bucket = "showvault-test",
                Region = "us-east-1",
                Prefix = "showvault/v1"
            }
        }), TimeProvider.System);

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

    private sealed class InMemoryHostedObjectStore : IHostedObjectStore
    {
        private readonly ConcurrentDictionary<string, byte[]> _objects =
            new(StringComparer.Ordinal);

        public bool Available { get; set; } = true;
        public IReadOnlyCollection<string> Keys => _objects.Keys.ToArray();

        public Task<byte[]?> ReadAsync(
            string key, int maximumBytes, CancellationToken cancellationToken)
        {
            RequireAvailable();
            if (!_objects.TryGetValue(key, out var bytes))
                return Task.FromResult<byte[]?>(null);
            if (bytes.Length > maximumBytes)
                throw new HostedSyncConflictException("A hosted object is oversized.");
            return Task.FromResult<byte[]?>(bytes.ToArray());
        }

        public Task<bool> PutIfAbsentAsync(
            string key, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            RequireAvailable();
            return Task.FromResult(_objects.TryAdd(key, bytes.ToArray()));
        }

        public Task<IReadOnlyList<HostedObjectInfo>> ListAsync(
            string prefix, CancellationToken cancellationToken)
        {
            RequireAvailable();
            IReadOnlyList<HostedObjectInfo> results = _objects
                .Where(item => item.Key.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new HostedObjectInfo(item.Key, item.Value.LongLength))
                .ToArray();
            return Task.FromResult(results);
        }

        public Task CheckAvailabilityAsync(CancellationToken cancellationToken)
        {
            RequireAvailable();
            return Task.CompletedTask;
        }

        public void Replace(string key, byte[] bytes) => _objects[key] = bytes.ToArray();

        private void RequireAvailable()
        {
            if (!Available)
                throw new HostedSyncUnavailableException("Object storage is unavailable.");
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "ShowVault.Api.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
