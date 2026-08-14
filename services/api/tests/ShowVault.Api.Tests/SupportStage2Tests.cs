using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Data;
using ShowVault.Api.Endpoints;
using ShowVault.Api.Support;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Support;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class SupportStage2Tests(TenantApiFactory factory) : IClassFixture<TenantApiFactory>
{
    private const string Issuer = "https://support-identity.showvault.test/";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");

    [Fact]
    public void Options_fail_closed_for_incomplete_unsafe_or_customer_audience()
    {
        Assert.Throws<InvalidOperationException>(() => new SupportAdminOptions { Enabled = true }
            .RequireValid("customer"));
        Assert.Throws<InvalidOperationException>(() => new SupportAdminOptions
        { Enabled = true, Authority = "http://support.test/", Audience = "support" }
            .RequireValid("customer"));
        Assert.Throws<InvalidOperationException>(() => new SupportAdminOptions
        { Enabled = true, Authority = $"https://{string.Join('.', Enumerable.Repeat(new string('a', 60), 5))}/", Audience = "support" }
            .RequireValid("customer"));
        Assert.Throws<InvalidOperationException>(() => new SupportAdminOptions
        { Enabled = true, Authority = Issuer, Audience = "customer" }
            .RequireValid("customer"));
        Assert.Throws<InvalidOperationException>(() => new SupportAdminOptions
        { Enabled = true, Authority = Issuer, Audience = "support\napi" }
            .RequireValid("customer"));
        Assert.Equal((Issuer, "support"), new SupportAdminOptions
        { Enabled = true, Authority = Issuer, Audience = " support " }
            .RequireValid("customer"));
    }

    [Fact]
    public void Step_up_requires_support_scheme_exact_issuer_scope_mfa_and_fresh_iat()
    {
        var time = new FixedTimeProvider(Now);
        var evaluator = new SupportStepUpAuthorization(time);
        Assert.True(evaluator.Evaluate(User(Now.ToUnixTimeSeconds()), Issuer).Authorized);
        Assert.True(evaluator.Evaluate(User(Now.ToUnixTimeSeconds(), mfa: "[\"pwd\",\"mfa\"]"), Issuer).Authorized);
        Assert.False(evaluator.Evaluate(User(Now.ToUnixTimeSeconds(), scheme: "Test"), Issuer).Authorized);
        Assert.False(evaluator.Evaluate(User(Now.ToUnixTimeSeconds(), issuer: "https://wrong.test/"), Issuer).Authorized);
        Assert.False(evaluator.Evaluate(User(Now.ToUnixTimeSeconds(), scope: "customer:read"), Issuer).Authorized);
        Assert.False(evaluator.Evaluate(User(Now.ToUnixTimeSeconds(), mfa: "[\"pwd\"]"), Issuer).Authorized);
        Assert.False(evaluator.Evaluate(User(Now.AddMinutes(-5).AddTicks(-1).ToUnixTimeSeconds()), Issuer).Authorized);
        Assert.False(evaluator.Evaluate(User(Now.AddSeconds(31).ToUnixTimeSeconds()), Issuer).Authorized);
        Assert.False(evaluator.Evaluate(User(-1), Issuer).Authorized);
    }

    [Fact]
    public async Task Rate_limit_is_partitioned_and_bounded()
    {
        var limiter = new SupportRequestRateLimiter(new FixedTimeProvider(Now));
        for (var count = 0; count < SupportRequestRateLimiter.PermitLimit; count++)
            Assert.True(limiter.TryAcquire(Issuer, "staff|one", "127.0.0.1"));
        Assert.False(limiter.TryAcquire(Issuer, "staff|one", "127.0.0.1"));
        Assert.True(limiter.TryAcquire(Issuer, "staff|one", "127.0.0.2"));
        Assert.True(limiter.TryAcquire(Issuer, "staff|two", "127.0.0.1"));

        var capacity = new SupportRequestRateLimiter(new FixedTimeProvider(Now));
        for (var count = 0; count < SupportRequestRateLimiter.MaximumPartitions; count++)
            Assert.True(capacity.TryAcquire(Issuer, $"staff|{count}", "127.0.0.1"));
        Assert.False(capacity.TryAcquire(Issuer, "staff|overflow", "127.0.0.1"));
        Assert.Equal(SupportRequestRateLimiter.MaximumPartitions, capacity.PartitionCount);

        var concurrent = new SupportRequestRateLimiter(new FixedTimeProvider(Now));
        for (var count = 0; count < SupportRequestRateLimiter.MaximumPartitions - 64; count++)
            Assert.True(concurrent.TryAcquire(Issuer, $"staff|seed-{count}", "127.0.0.1"));
        var attempts = Enumerable.Range(0, 128).Select(count => Task.Run(() =>
            concurrent.TryAcquire(Issuer, $"staff|race-{count}", "127.0.0.1"))).ToArray();
        var results = await Task.WhenAll(attempts);
        Assert.Equal(64, results.Count(result => result));
        Assert.Equal(SupportRequestRateLimiter.MaximumPartitions, concurrent.PartitionCount);

        var time = new MutableTimeProvider(Now);
        var expiry = new SupportRequestRateLimiter(time);
        Assert.True(expiry.TryAcquire(Issuer, "staff|active", "127.0.0.1"));
        for (var count = 1; count < SupportRequestRateLimiter.MaximumPartitions; count++)
            Assert.True(expiry.TryAcquire(Issuer, $"staff|stale-{count}", "127.0.0.1"));
        time.Now = Now + SupportRequestRateLimiter.Retention + TimeSpan.FromSeconds(1);
        Assert.True(expiry.TryAcquire(Issuer, "staff|active", "127.0.0.1"));
        Assert.True(expiry.TryAcquire(Issuer, "staff|replacement", "127.0.0.1"));
        Assert.Equal(2, expiry.PartitionCount);
        for (var count = 0; count < SupportRequestRateLimiter.PermitLimit - 1; count++)
            Assert.True(expiry.TryAcquire(Issuer, "staff|active", "127.0.0.1"));
        Assert.False(expiry.TryAcquire(Issuer, "staff|active", "127.0.0.1"));
    }

    [Fact]
    public async Task Strict_body_rejects_lookalike_media_unknown_duplicate_empty_and_oversize()
    {
        var id = Guid.NewGuid();
        Assert.Equal(id, (await Parse($"{{\"organizationId\":\"{id:D}\"}}",
            "application/json; charset=utf-8"))!.OrganizationId);
        Assert.Null(await Parse($"{{\"organizationId\":\"{id:D}\"}}", "application/jsonp"));
        Assert.Null(await Parse($"{{\"organizationId\":\"{id:D}\",\"extra\":1}}"));
        Assert.Null(await Parse($"{{\"organizationId\":\"{id:D}\",\"organizationId\":\"{id:D}\"}}"));
        Assert.Null(await Parse("{\"organizationId\":\"00000000-0000-0000-0000-000000000000\"}"));
        Assert.Null(await Parse("{\"organizationId\":\"" + new string('a', 4100) + "\"}"));
    }

    [Fact]
    public async Task Disabled_route_is_absent_and_enabled_route_inventory_has_one_support_post()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClient().PostAsync("/api/v1/support/organization-overview",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
        using var enabled = TenantApiFactory.WithSupportEnabled();
        var routes = enabled.Services.GetServices<EndpointDataSource>().SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Where(endpoint =>
                endpoint.RoutePattern.RawText?.Contains("/support/", StringComparison.Ordinal) == true)
            .ToArray();
        var route = Assert.Single(routes);
        Assert.Equal("/api/v1/support/organization-overview", route.RoutePattern.RawText);
        Assert.Contains("HTTP: POST", route.DisplayName);
        Assert.Equal(SupportAdminOptions.SchemeName,
            route.Metadata.GetMetadata<IAuthorizeData>()!.AuthenticationSchemes);
        var challenge = await enabled.CreateClient().PostAsync(
            "/api/v1/support/organization-overview",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.Contains("no-store", challenge.Headers.CacheControl!.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Service_returns_fixed_minimized_projection_and_commits_one_audit()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = Organization.Create("Synthetic support target", $"support-{Guid.NewGuid():N}");
        var assignment = SupportStaffAssignment.Create(Issuer, $"staff|{Guid.NewGuid():N}", Now);
        database.AddRange(organization, assignment,
            SupportOrganizationGrant.Create(assignment.Id, organization.Id, Now),
            Membership.Create(organization.Id, $"member|{Guid.NewGuid():N}",
                OrganizationRole.Owner, Now));
        await database.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<SupportOrganizationOverviewService>();
        var result = await service.GetAsync(organization.Id, assignment.IdentityIssuer,
            assignment.IdentitySubject, "correlation-fixture", default);

        Assert.True(result.Kind == SupportOverviewResultKind.Success, result.ReasonCode);
        Assert.Equal(15, result.Value!.Members.Count);
        Assert.Equal(2, result.Value.HostedSync.Counts.Count);
        Assert.Equal(["uploading", "completed"],
            result.Value.HostedSync.Counts.Select(value => value.Status));
        Assert.Equal(1, result.Value.Members.Single(value =>
            value.Role == "owner" && value.State == "active").Count);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("IdentitySubject", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProviderCustomer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Manifest", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Correlation", json, StringComparison.OrdinalIgnoreCase);
        var audit = await database.SupportAuditEvents.SingleAsync(value =>
            value.CorrelationId == "correlation-fixture");
        Assert.Equal(organization.Id, audit.OrganizationId);
        Assert.Equal("allowed", audit.Outcome);
    }

    [Fact]
    public async Task Unknown_and_ungranted_targets_share_result_query_count_and_null_tenant_audit()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var assignment = SupportStaffAssignment.Create(Issuer, $"staff|{Guid.NewGuid():N}", Now);
        var ungranted = Organization.Create("Synthetic ungranted", $"ungranted-{Guid.NewGuid():N}");
        database.AddRange(assignment, ungranted);
        await database.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<SupportOrganizationOverviewService>();

        _ = await service.GetAsync(Guid.NewGuid(), Issuer, assignment.IdentitySubject,
            $"warm-{Guid.NewGuid():N}", default);
        factory.Commands.Reset();
        var unknown = await service.GetAsync(Guid.NewGuid(), Issuer, assignment.IdentitySubject,
            $"unknown-{Guid.NewGuid():N}", default);
        var unknownQueries = factory.Commands.Reset();
        var ungrantedResult = await service.GetAsync(ungranted.Id, Issuer, assignment.IdentitySubject,
            $"ungranted-{Guid.NewGuid():N}", default);
        var ungrantedQueries = factory.Commands.Reset();

        Assert.Equal(SupportOverviewResultKind.TargetUnavailable, unknown.Kind);
        Assert.Equal(unknown.Kind, ungrantedResult.Kind);
        Assert.Equal(unknown.ReasonCode, ungrantedResult.ReasonCode);
        Assert.Equal(unknownQueries, ungrantedQueries);
        var audits = await database.SupportAuditEvents.Where(value =>
            value.ActorSubject == assignment.IdentitySubject).ToListAsync();
        Assert.Equal(3, audits.Count);
        Assert.All(audits, audit => Assert.Null(audit.OrganizationId));
        Assert.All(audits, audit => Assert.Equal("support_target_unavailable", audit.ReasonCode));
    }

    private static ClaimsPrincipal User(long issuedAt, string scheme = SupportAdminOptions.SchemeName,
        string issuer = Issuer, string scope = SupportAdminOptions.RequiredScope, string mfa = "mfa")
    {
        var claims = new[] { new Claim("iss", issuer), new Claim("sub", "staff|reader"),
            new Claim("scope", scope),
            new Claim(SupportStepUpAuthorization.AuthenticationMethodsClaim, mfa),
            new Claim("iat", issuedAt.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        return new(new ClaimsIdentity(claims, scheme));
    }

    private static async Task<ShowVault.Api.Contracts.SupportOrganizationOverviewRequest?> Parse(
        string body, string contentType = "application/json")
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
        return await SupportEndpoints.ParseAsync(context.Request, default);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
