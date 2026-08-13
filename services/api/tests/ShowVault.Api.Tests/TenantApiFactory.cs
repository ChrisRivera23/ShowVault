using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShowVault.Api.Data;
using ShowVault.Api.Billing;
using ShowVault.Api.Account;
using ShowVault.Platform.Billing;

namespace ShowVault.Api.Tests;

public sealed class TenantApiFactory : WebApplicationFactory<Program>
{
    public SyntheticBillingProvider BillingProvider { get; } = new();
    private readonly SqliteConnection _connection = new(
        $"Data Source=showvault-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=30");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PlatformDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PlatformDbContext>>();
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseSqlite(_connection.ConnectionString));
            services.RemoveAll<IBillingProvider>();
            services.RemoveAll<IBillingOfferingCatalog>();
            services.AddSingleton<IBillingProvider>(BillingProvider);
            services.AddSingleton<IBillingOfferingCatalog, TestBillingOfferingCatalog>();
            services.PostConfigure<BillingOptions>(options =>
            {
                options.Enabled = true;
                options.Environment = BillingProviderEnvironment.Sandbox;
                options.ReturnOrigin = "https://account.showvault.test/";
                options.ProviderApiVersion = "2026-07-01.fixture";
                options.CheckoutLifetimeMinutes = 30;
            });
            services.PostConfigure<StripeWebhookOptions>(options =>
            {
                options.EndpointSecrets = ["whsec_local_fixture_only"];
                options.TimestampToleranceSeconds = 300;
            });
            services.PostConfigure<AccountInvitationOptions>(options =>
            {
                options.Enabled = true;
                options.LifetimeHours = 168;
                options.ActiveKeyId = "fixture-active";
                options.Keys =
                [
                    new AccountInvitationKeyOptions
                    {
                        Id = "fixture-active",
                        SecretBase64 = Convert.ToBase64String(Enumerable.Range(1, 32)
                            .Select(value => (byte)value).ToArray())
                    }
                ];
                options.MaximumCodeBytes = 64;
            });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });
        });

        builder.ConfigureServices(services =>
        {
            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

public sealed class TestBillingOfferingCatalog : IBillingOfferingCatalog
{
    public static BillingOffering Offering { get; } = new(
        "showvault-standard", "ShowVault standard", "synthetic.standard",
        "showvault.perpetual", "price_recurring_fixture", "price_license_fixture",
        "billing-fixture-1");
    public BillingOffering? Find(string code) => code == Offering.Code ? Offering : null;
    public BillingOffering? Current => Offering;
}

public sealed class SyntheticBillingProvider : IBillingProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, BillingHostedSession> _checkout = [];
    public bool IsAvailable => true;
    public int CheckoutCreationCount { get; private set; }
    public int PortalCreationCount { get; private set; }
    public BillingProviderSnapshot? Snapshot { get; set; }

    public Task<BillingHostedSession> CreateCheckoutAsync(BillingCheckoutCommand command,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_checkout.TryGetValue(idempotencyKey, out var existing))
                return Task.FromResult(existing);
            CheckoutCreationCount++;
            var session = new BillingHostedSession("cs_test_" + command.AttemptId.ToString("N"),
                new Uri("https://checkout.stripe.test/session/" + command.AttemptId.ToString("N")),
                DateTimeOffset.UtcNow.AddMinutes(30));
            _checkout[idempotencyKey] = session;
            return Task.FromResult(session);
        }
    }

    public Task<BillingHostedSession> CreatePortalAsync(string customerId, Uri returnUrl,
        CancellationToken cancellationToken)
    {
        PortalCreationCount++;
        return Task.FromResult(new BillingHostedSession("bps_test_fixture",
            new Uri("https://billing.stripe.test/session/fixture"),
            DateTimeOffset.UtcNow.AddMinutes(5)));
    }

    public Task<BillingProviderSnapshot?> RetrieveCurrentStateAsync(string eventType,
        string providerObjectId, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot);
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Subject", out var subject) ||
            string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authenticationType = Request.Headers.TryGetValue("X-Test-Authentication-Type",
            out var requestedType) && !string.IsNullOrWhiteSpace(requestedType)
            ? requestedType.ToString() : SchemeName;
        var claims = new List<Claim> { new("sub", subject.ToString()) };
        if (Request.Headers.TryGetValue("X-Test-Scope", out var scope))
            claims.Add(new Claim("scope", scope.ToString()));
        if (Request.Headers.TryGetValue("X-Test-Mfa", out var mfa))
            claims.Add(new Claim(
                "https://showvault.app/authentication_methods", mfa.ToString()));
        if (Request.Headers.TryGetValue("X-Test-Iat", out var issuedAt))
            claims.Add(new Claim("iat", issuedAt.ToString()));
        var identity = new ClaimsIdentity(claims, authenticationType);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
