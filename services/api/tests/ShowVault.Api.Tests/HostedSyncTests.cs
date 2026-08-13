using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.HostedSync;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Venues;
using LocalCatalogAuthorizer = ShowVault.LocalEngine.LocalCatalogAuthorizer;
using LocalRecoveryEngine = ShowVault.LocalEngine.LocalRecoveryEngine;
using LocalSyncEngine = ShowVault.LocalEngine.LocalSyncEngine;
using LocalSaveRequest = ShowVault.LocalEngine.LocalSaveRequest;
using LocalSyncRequest = ShowVault.LocalEngine.LocalSyncRequest;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class HostedSyncTests(TenantApiFactory factory) : IClassFixture<TenantApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Owner_can_resume_identical_chunk_and_commit_receipt_last()
    {
        var tenant = await CreateTenantAsync("owner");
        var content = Encoding.UTF8.GetBytes("synthetic backup");
        var beginRequest = BeginRequest(content);
        var client = Client("owner");
        var root = Root(tenant, beginRequest.Manifest.RecoveryPointId);

        var begin = await client.PostAsJsonAsync(root + "/begin", beginRequest);
        Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
        var session = (await begin.Content.ReadFromJsonAsync<ApiResponse<HostedSyncBeginResponse>>())!
            .Payload;
        Assert.False(session.Completed);

        var path = Uri.EscapeDataString(beginRequest.Manifest.Files[0].RelativePath);
        var state = await client.GetFromJsonAsync<ApiResponse<HostedSyncFileStateResponse>>(
            root + $"/sessions/{session.SessionId}/files?path={path}");
        Assert.Equal(0, state!.Payload.NextOffset);

        await PutChunkAsync(client, root, session.SessionId, path, 0, content);
        await PutChunkAsync(client, root, session.SessionId, path, 0, content);
        var conflictingBytes = Enumerable.Repeat((byte)'x', content.Length).ToArray();
        using (var conflict = new HttpRequestMessage(HttpMethod.Put,
            root + $"/sessions/{session.SessionId}/files?path={path}&offset=0")
        {
            Content = new ByteArrayContent(conflictingBytes)
        })
        {
            conflict.Headers.Add("X-ShowVault-Chunk-Sha256",
                Convert.ToHexStringLower(SHA256.HashData(conflictingBytes)));
            conflict.Content.Headers.ContentType = new("application/octet-stream");
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.SendAsync(conflict)).StatusCode);
        }
        var resumed = await client.GetFromJsonAsync<ApiResponse<HostedSyncFileStateResponse>>(
            root + $"/sessions/{session.SessionId}/files?path={path}");
        Assert.Equal(content.Length, resumed!.Payload.NextOffset);

        var commit = await client.PostAsJsonAsync(
            root + $"/sessions/{session.SessionId}/commit", new { });
        var receipt = (await commit.Content.ReadFromJsonAsync<ApiResponse<HostedSyncReceipt>>())!
            .Payload;
        Assert.Equal(tenant.OrganizationId, receipt.OrganizationId);
        Assert.Equal(tenant.VenueId, receipt.VenueId);
        Assert.Equal(beginRequest.ManifestDigest, receipt.ManifestDigest);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)),
            Assert.Single(receipt.Objects).Sha256);

        var repeated = await client.PostAsJsonAsync(
            root + $"/sessions/{session.SessionId}/commit", new { });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var fetched = await client.GetAsync(root + "/receipt");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Commit_rejects_missing_or_corrupt_content_without_receipt()
    {
        var tenant = await CreateTenantAsync("integrity-owner");
        var request = BeginRequest(Encoding.UTF8.GetBytes("expected"));
        var client = Client("integrity-owner");
        var root = Root(tenant, request.Manifest.RecoveryPointId);
        var begin = (await (await client.PostAsJsonAsync(root + "/begin", request))
            .Content.ReadFromJsonAsync<ApiResponse<HostedSyncBeginResponse>>())!.Payload;

        var commit = await client.PostAsJsonAsync(
            root + $"/sessions/{begin.SessionId}/commit", new { });
        Assert.Equal(HttpStatusCode.Conflict, commit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(root + "/receipt")).StatusCode);
    }

    [Fact]
    public async Task Zero_byte_file_commits_without_a_chunk_request()
    {
        var tenant = await CreateTenantAsync("zero-owner");
        var request = BeginRequest([]);
        var client = Client("zero-owner");
        var root = Root(tenant, request.Manifest.RecoveryPointId);
        var begin = (await (await client.PostAsJsonAsync(root + "/begin", request))
            .Content.ReadFromJsonAsync<ApiResponse<HostedSyncBeginResponse>>())!.Payload;

        var commit = await client.PostAsJsonAsync(
            root + $"/sessions/{begin.SessionId}/commit", new { });

        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        var receipt = (await commit.Content.ReadFromJsonAsync<ApiResponse<HostedSyncReceipt>>())!
            .Payload;
        Assert.Equal(0, Assert.Single(receipt.Objects).Size);
    }

    [Theory]
    [InlineData(OrganizationRole.Viewer)]
    [InlineData(OrganizationRole.Technician)]
    public async Task Read_only_roles_cannot_begin_sync(OrganizationRole role)
    {
        var subject = $"denied-{role}";
        var tenant = await CreateTenantAsync("role-owner");
        await AddMembershipAsync(tenant.OrganizationId, subject, role);
        var request = BeginRequest(Encoding.UTF8.GetBytes("synthetic"));

        var response = await Client(subject).PostAsJsonAsync(
            Root(tenant, request.Manifest.RecoveryPointId) + "/begin", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationRole.Manager)]
    [InlineData(OrganizationRole.Administrator)]
    public async Task Management_roles_can_begin_sync(OrganizationRole role)
    {
        var subject = $"allowed-{role}";
        var tenant = await CreateTenantAsync("management-owner");
        await AddMembershipAsync(tenant.OrganizationId, subject, role);
        var request = BeginRequest(Encoding.UTF8.GetBytes("synthetic"));

        var response = await Client(subject).PostAsJsonAsync(
            Root(tenant, request.Manifest.RecoveryPointId) + "/begin", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manifest_rejects_unsafe_paths_and_unapproved_metadata()
    {
        var tenant = await CreateTenantAsync("validation-owner");
        var content = Encoding.UTF8.GetBytes("synthetic");
        var valid = BeginRequest(content);
        var unsafeManifest = valid.Manifest with
        {
            PluginId = "client.chosen",
            Files = [valid.Manifest.Files[0] with { RelativePath = "../private" }]
        };
        var json = JsonSerializer.Serialize(unsafeManifest, JsonOptions);
        var request = new HostedSyncBeginRequest(unsafeManifest,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))));

        var response = await Client("validation-owner").PostAsJsonAsync(
            Root(tenant, request.Manifest.RecoveryPointId) + "/begin", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Begin_rejects_extra_path_metadata_and_outsider_tenant_access()
    {
        var tenant = await CreateTenantAsync("closed-owner");
        var request = BeginRequest(Encoding.UTF8.GetBytes("synthetic"));
        var json = JsonSerializer.Serialize(request, JsonOptions);
        json = json.Replace("\"manifestDigest\"", "\"sourcePath\":\"/private/customer\",\"manifestDigest\"",
            StringComparison.Ordinal);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var root = Root(tenant, request.Manifest.RecoveryPointId);

        var extra = await Client("closed-owner").PostAsync(root + "/begin", content);
        var outsider = await Client("outsider").PostAsJsonAsync(root + "/begin", request);

        Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
    }

    [Fact]
    public async Task Local_engine_to_api_synthetic_round_trip_records_synchronized_receipt()
    {
        var root = Path.Combine(Path.GetTempPath(), "showvault-sync-e2e", Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "home");
        var source = Path.Combine(home, "Music", "_Serato_");
        var vault = Path.Combine(root, "vault");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "database V2"), "synthetic library");
        try
        {
            var tenant = await CreateTenantAsync("e2e-owner");
            var local = new LocalRecoveryEngine(
                new LocalCatalogAuthorizer(new Dictionary<string, string>(), home));
            var saved = await local.SaveAsync(new LocalSaveRequest(
                "macos.serato-dj-pro.user-data", source, vault));
            var client = Client("e2e-owner");
            var result = await new LocalSyncEngine(client).SynchronizeAsync(new LocalSyncRequest(
                vault, tenant.OrganizationId, tenant.VenueId, "synthetic-access-token",
                client.BaseAddress!));

            Assert.Equal(1, result.SynchronizedCount);
            Assert.Equal(saved.TotalBytes, result.SynchronizedBytes);
            var point = Assert.Single(await local.InspectVaultAsync(vault));
            Assert.Equal("synchronized", point.CloudStatus);
            var receipt = await client.GetAsync(Root(tenant, saved.RecoveryPointId) + "/receipt");
            Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private HttpClient Client(string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        return client;
    }

    private async Task<(Guid OrganizationId, Guid VenueId)> CreateTenantAsync(string subject)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = Organization.Create("Synthetic", $"sync-{Guid.NewGuid():N}");
        var venue = Venue.Create(organization.Id, "Synthetic Venue", "UTC");
        database.Organizations.Add(organization);
        database.Venues.Add(venue);
        database.Memberships.Add(Membership.Create(organization.Id, subject,
            OrganizationRole.Owner));
        await database.SaveChangesAsync();
        return (organization.Id, venue.Id);
    }

    private async Task AddMembershipAsync(Guid organizationId, string subject, OrganizationRole role)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Memberships.Add(Membership.Create(organizationId, subject, role));
        await database.SaveChangesAsync();
    }

    private static HostedSyncBeginRequest BeginRequest(byte[] content)
    {
        var contentDigest = Convert.ToHexStringLower(SHA256.HashData(content));
        var localSeed = SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));
        var recoveryPointId = Convert.ToHexStringLower(localSeed);
        var manifest = new HostedSyncManifest("1.0", recoveryPointId, recoveryPointId,
            "macos.serato-dj-pro.user-data", "showvault.serato-dj-pro",
            DateTimeOffset.Parse("2026-08-13T12:00:00Z"), 1, content.Length,
            [new("Subcrates/synthetic.crate", content.Length, contentDigest)]);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        return new(manifest,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))));
    }

    private static async Task PutChunkAsync(HttpClient client, string root,
        string sessionId, string path, long offset, byte[] bytes)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            root + $"/sessions/{sessionId}/files?path={path}&offset={offset}")
        {
            Content = new ByteArrayContent(bytes)
        };
        request.Headers.Add("X-ShowVault-Chunk-Sha256",
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
        request.Content.Headers.ContentType = new("application/octet-stream");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static string Root((Guid OrganizationId, Guid VenueId) tenant, string recoveryPointId) =>
        $"/api/v1/organizations/{tenant.OrganizationId}/venues/{tenant.VenueId}" +
        $"/hosted-sync/{recoveryPointId}";
}
