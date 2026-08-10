using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Platform.Organizations;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class HostedSyncTests(TenantApiFactory factory)
    : IClassFixture<TenantApiFactory>
{
    [Fact]
    public async Task Authenticated_owner_can_resume_verify_and_idempotently_commit()
    {
        var (client, organizationId, venueId) = await CreateTenantAsync("sync-owner");
        using (client)
        {
            var content = Encoding.UTF8.GetBytes("synthetic hosted bytes");
            var packageId = Digest("local manifest identity");
            var manifest = Manifest(packageId, content);
            var path = BasePath(organizationId, venueId, packageId);

            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync($"{path}/begin",
                    new BeginHostedSyncRequest(manifest))).StatusCode);

            var initial = await client.PostAsJsonAsync($"{path}/file-state",
                new HostedSyncFileStateRequest("database V2"));
            var initialBody = await initial.Content.ReadFromJsonAsync<
                ApiResponse<HostedSyncFileStateResponse>>();
            Assert.Equal(0, initialBody!.Payload.UploadedLength);

            var first = content[..8];
            var firstRequest = new AppendHostedSyncChunkRequest("database V2", 0, first);
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync($"{path}/chunks", firstRequest)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync($"{path}/chunks", firstRequest)).StatusCode);

            var resumed = await client.PostAsJsonAsync($"{path}/file-state",
                new HostedSyncFileStateRequest("database V2"));
            var resumedBody = await resumed.Content.ReadFromJsonAsync<
                ApiResponse<HostedSyncFileStateResponse>>();
            Assert.Equal(8, resumedBody!.Payload.UploadedLength);

            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync($"{path}/chunks",
                    new AppendHostedSyncChunkRequest("database V2", 8, content[8..]))).StatusCode);

            var commit = await client.PostAsJsonAsync($"{path}/commit",
                new BeginHostedSyncRequest(manifest));
            Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
            var receipt = await commit.Content.ReadFromJsonAsync<
                ApiResponse<HostedSyncReceiptResponse>>();
            Assert.Equal(packageId, receipt!.Payload.PackageId);
            Assert.Equal(Digest(manifest), receipt.Payload.RemoteManifestSha256);

            var duplicate = await client.PostAsJsonAsync($"{path}/commit",
                new BeginHostedSyncRequest(manifest));
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
            var duplicateReceipt = await duplicate.Content.ReadFromJsonAsync<
                ApiResponse<HostedSyncReceiptResponse>>();
            Assert.Equal(receipt.Payload.CompletedAt, duplicateReceipt!.Payload.CompletedAt);

            var racedState = await client.PostAsJsonAsync($"{path}/file-state",
                new HostedSyncFileStateRequest("database V2"));
            var racedStateBody = await racedState.Content.ReadFromJsonAsync<
                ApiResponse<HostedSyncFileStateResponse>>();
            Assert.Equal(content.Length, racedStateBody!.Payload.UploadedLength);
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync($"{path}/chunks", firstRequest)).StatusCode);
        }
    }

    [Fact]
    public async Task Missing_outsider_and_viewer_sessions_cannot_begin_uploads()
    {
        var (owner, organizationId, venueId) = await CreateTenantAsync("sync-auth");
        using (owner)
        using (var missing = factory.CreateClient())
        using (var outsider = Client("auth0|hosted-outsider"))
        using (var viewer = Client("auth0|hosted-viewer"))
        {
            await AddMembershipAsync(organizationId, "auth0|hosted-viewer", OrganizationRole.Viewer);
            var packageId = Digest("auth package");
            var request = new BeginHostedSyncRequest(
                Manifest(packageId, Encoding.UTF8.GetBytes("bytes")));
            var path = $"{BasePath(organizationId, venueId, packageId)}/begin";

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await missing.PostAsJsonAsync(path, request)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await outsider.PostAsJsonAsync(path, request)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await viewer.PostAsJsonAsync(path, request)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,
                (await owner.PostAsJsonAsync(path, request)).StatusCode);
        }
    }

    [Fact]
    public async Task Same_package_identity_is_isolated_between_tenants()
    {
        var first = await CreateTenantAsync("sync-tenant-a");
        var second = await CreateTenantAsync("sync-tenant-b");
        using (first.Client)
        using (second.Client)
        {
            var packageId = Digest("shared package id");
            var content = Encoding.UTF8.GetBytes("tenant A only");
            var manifest = Manifest(packageId, content);
            var firstPath = BasePath(first.OrganizationId, first.VenueId, packageId);
            await first.Client.PostAsJsonAsync($"{firstPath}/begin",
                new BeginHostedSyncRequest(manifest));
            await first.Client.PostAsJsonAsync($"{firstPath}/chunks",
                new AppendHostedSyncChunkRequest("database V2", 0, content));
            Assert.Equal(HttpStatusCode.OK,
                (await first.Client.PostAsJsonAsync($"{firstPath}/commit",
                    new BeginHostedSyncRequest(manifest))).StatusCode);

            var secondPath = BasePath(second.OrganizationId, second.VenueId, packageId);
            Assert.Equal(HttpStatusCode.NotFound,
                (await second.Client.GetAsync($"{secondPath}/receipt")).StatusCode);
        }
    }

    [Fact]
    public async Task Corrupt_content_never_publishes_a_receipt()
    {
        var tenant = await CreateTenantAsync("sync-tamper");
        using (tenant.Client)
        {
            var expected = Encoding.UTF8.GetBytes("expected");
            var packageId = Digest("tamper package");
            var manifest = Manifest(packageId, expected);
            var path = BasePath(tenant.OrganizationId, tenant.VenueId, packageId);
            await tenant.Client.PostAsJsonAsync($"{path}/begin",
                new BeginHostedSyncRequest(manifest));
            await tenant.Client.PostAsJsonAsync($"{path}/chunks",
                new AppendHostedSyncChunkRequest(
                    "database V2", 0, Encoding.UTF8.GetBytes("tampered")));

            Assert.Equal(HttpStatusCode.Conflict,
                (await tenant.Client.PostAsJsonAsync($"{path}/commit",
                    new BeginHostedSyncRequest(manifest))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await tenant.Client.GetAsync($"{path}/receipt")).StatusCode);
        }
    }

    [Fact]
    public async Task Manifest_rejects_paths_and_unapproved_or_extra_metadata()
    {
        var tenant = await CreateTenantAsync("sync-privacy");
        using (tenant.Client)
        {
            var packageId = Digest("privacy package");
            var unsafeManifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                formatVersion = "showvault.remote-package.v1",
                packageId,
                createdAt = DateTimeOffset.UtcNow,
                source = new
                {
                    candidateKey = "macos.serato-dj-pro.user-data",
                    pluginId = "showvault.serato-dj-pro",
                    productName = "Serato DJ Pro",
                    sourcePath = "/private/customer/path"
                },
                files = new[]
                {
                    new { relativePath = "../escape", size = 1, sha256 = Digest("x") }
                },
                localManifestSha256 = packageId
            });
            var response = await tenant.Client.PostAsJsonAsync(
                $"{BasePath(tenant.OrganizationId, tenant.VenueId, packageId)}/begin",
                new BeginHostedSyncRequest(unsafeManifest));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(Directory.Exists(Path.Combine(
                factory.HostedSyncRoot,
                tenant.OrganizationId.ToString("N"),
                tenant.VenueId.ToString("N"))));
        }
    }

    [Fact]
    public async Task Zero_byte_files_are_materialized_and_verified()
    {
        var tenant = await CreateTenantAsync("sync-empty-file");
        using (tenant.Client)
        {
            var packageId = Digest("empty file package");
            var manifest = Manifest(packageId, []);
            var path = BasePath(tenant.OrganizationId, tenant.VenueId, packageId);
            await tenant.Client.PostAsJsonAsync($"{path}/begin",
                new BeginHostedSyncRequest(manifest));
            var state = await tenant.Client.PostAsJsonAsync($"{path}/file-state",
                new HostedSyncFileStateRequest("database V2"));
            Assert.Equal(HttpStatusCode.OK, state.StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await tenant.Client.PostAsJsonAsync($"{path}/commit",
                    new BeginHostedSyncRequest(manifest))).StatusCode);
        }
    }

    [Fact]
    public async Task Linked_server_storage_is_rejected_without_following_it()
    {
        if (OperatingSystem.IsWindows()) return;
        var tenant = await CreateTenantAsync("sync-link");
        using (tenant.Client)
        {
            var tenantRoot = Path.Combine(
                factory.HostedSyncRoot,
                tenant.OrganizationId.ToString("N"),
                tenant.VenueId.ToString("N"));
            Directory.CreateDirectory(tenantRoot);
            var outside = Directory.CreateDirectory(Path.Combine(
                Path.GetTempPath(), $"showvault-hosted-outside-{Guid.NewGuid():N}"));
            var partialLink = new DirectoryInfo(Path.Combine(tenantRoot, ".partial"));
            partialLink.CreateAsSymbolicLink(outside.FullName);
            try
            {
                var packageId = Digest("link package");
                var response = await tenant.Client.PostAsJsonAsync(
                    $"{BasePath(tenant.OrganizationId, tenant.VenueId, packageId)}/begin",
                    new BeginHostedSyncRequest(Manifest(packageId, Encoding.UTF8.GetBytes("x"))));
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Empty(outside.EnumerateFileSystemInfos());
            }
            finally
            {
                partialLink.Delete();
                outside.Delete(recursive: true);
            }
        }
    }

    private async Task<(HttpClient Client, Guid OrganizationId, Guid VenueId)> CreateTenantAsync(
        string suffix)
    {
        var client = Client($"auth0|{suffix}");
        var unique = $"{suffix}-{Guid.NewGuid():N}";
        var organizationResponse = await client.PostAsJsonAsync(
            "/api/v1/organizations", new CreateOrganizationRequest(unique, unique));
        var organization = await organizationResponse.Content.ReadFromJsonAsync<
            ApiResponse<OrganizationSummary>>();
        var venueResponse = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization!.Payload.Id}/venues",
            new CreateVenueRequest("Main Room", "America/New_York"));
        var venue = await venueResponse.Content.ReadFromJsonAsync<ApiResponse<VenueSummary>>();
        return (client, organization.Payload.Id, venue!.Payload.Id);
    }

    private HttpClient Client(string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        return client;
    }

    private async Task AddMembershipAsync(Guid organizationId, string subject, OrganizationRole role)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Memberships.Add(Membership.Create(organizationId, subject, role));
        await database.SaveChangesAsync();
    }

    private static string BasePath(Guid organizationId, Guid venueId, string packageId) =>
        $"/api/v1/organizations/{organizationId}/venues/{venueId}/hosted-sync/{packageId}";

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
                new { relativePath = "database V2", size = content.Length, sha256 = Digest(content) }
            },
            localManifestSha256 = packageId
        });

    private static string Digest(string value) => Digest(Encoding.UTF8.GetBytes(value));

    private static string Digest(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}
