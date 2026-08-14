using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ShowVault.SupportAdmin.Clients;
using ShowVault.SupportAdmin.Configuration;
using ShowVault.SupportAdmin.Security;
using Xunit;

namespace ShowVault.SupportAdmin.Tests;

public sealed class SupportAdminSecurityTests
{
    [Fact]
    public async Task Checked_in_configuration_serves_only_generic_disabled_response()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/", "/anything", "/support/signin-oidc" })
        {
            var response = await client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("Support administration is disabled", body, StringComparison.Ordinal);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.DoesNotContain("OidcClient", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("http://support.showvault.test/")]
    [InlineData("https://user@example.test/")]
    [InlineData("https://example.test/path")]
    [InlineData("https://example.test/?query=1")]
    public void Origins_must_be_exact_https_roots(string value) =>
        Assert.False(SupportAdminPortalOptions.HttpsRoot(value));

    [Fact]
    public void Enabled_configuration_is_development_only_and_complete()
    {
        var options = Complete();
        Assert.True(options.IsComplete(development: true));
        Assert.False(options.IsComplete(development: false));
        options.ApiBaseUri = options.Origin;
        Assert.False(options.IsComplete(development: true));
    }

    [Fact]
    public void Enabled_non_development_startup_fails_closed()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            ConfigureComplete(builder);
        });
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public async Task Ticket_handle_is_opaque_bounded_and_expires()
    {
        var now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");
        var time = new MutableTimeProvider(now);
        var tickets = new SupportServerSideTicketStore(time);
        var properties = new AuthenticationProperties { ExpiresUtc = now.AddMinutes(5) };
        properties.StoreTokens([new AuthenticationToken
        {
            Name = "access_token", Value = "support-access-token-fixture"
        }]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(
            new ClaimsIdentity("Test")), properties, SupportAdminPortalOptions.CookieScheme);
        var handles = new List<string>();
        for (var index = 0; index < 4100; index++) handles.Add(await tickets.StoreAsync(ticket));
        Assert.Equal(4096, (await Task.WhenAll(handles.Select(async handle =>
            await tickets.RetrieveAsync(handle) is not null))).Count(value => value));
        Assert.All(handles, handle =>
            Assert.DoesNotContain("support-access-token-fixture", handle, StringComparison.Ordinal));
        await tickets.RemoveAsync(handles[^1]);
        Assert.Null(await tickets.RetrieveAsync(handles[^1]));
        time.Now = now.AddMinutes(6);
        Assert.Null(await tickets.RetrieveAsync(handles[^2]));
    }

    [Fact]
    public async Task Enabled_portal_enforces_origin_and_fresh_mfa_oidc_contract()
    {
        await using var factory = new EnabledSupportFactory();
        using var wrong = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://evil.showvault.test/")
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await wrong.GetAsync("/")).StatusCode);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://support.showvault.test/")
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Host", "evil.showvault.test");
        var challenge = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
            challenge.Headers.Location!.Query);
        Assert.Equal("https://support-api.showvault.test", query["audience"]);
        Assert.Equal(["openid", SupportAdminPortalOptions.RequiredScope],
            query["scope"].ToString().Split(' '));
        Assert.DoesNotContain("offline_access", query["scope"].ToString(),
            StringComparison.Ordinal);
        Assert.Equal("0", query["max_age"]);
        Assert.Equal("http://schemas.openid.net/pape/policies/2007/06/multi-factor",
            query["acr_values"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("https://support.showvault.test/support/signin-oidc",
            query["redirect_uri"]);
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
        Assert.False(string.IsNullOrWhiteSpace(query["nonce"]));
    }

    [Fact]
    public async Task Exact_id_post_is_csrf_protected_and_renders_only_minimized_response()
    {
        await using var factory = new EnabledSupportFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://support.showvault.test/")
        });
        client.DefaultRequestHeaders.Add("X-Support-Test-Auth", "1");
        var html = await client.GetStringAsync("/");
        Assert.DoesNotContain("support-access-token-fixture", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync("/", new FormUrlEncodedContent([]))).StatusCode);

        var response = await client.PostAsync("/", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(html),
                ["OrganizationId"] = factory.Api.OrganizationId.ToString("D")
            }));
        var rendered = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("Synthetic support organization", rendered, StringComparison.Ordinal);
        Assert.Contains(factory.Api.OrganizationId.ToString("D"), rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("support-access-token-fixture", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("provider-customer-fixture", rendered, StringComparison.Ordinal);
        Assert.Equal("Bearer support-access-token-fixture", factory.Api.LastAuthorization);
        Assert.Equal("POST", factory.Api.LastMethod);
        Assert.Equal("/api/v1/support/organization-overview", factory.Api.LastPath);
        Assert.Equal($"{{\"organizationId\":\"{factory.Api.OrganizationId:D}\"}}",
            factory.Api.LastBody);
        var refreshed = await client.GetStringAsync("/");
        Assert.DoesNotContain(factory.Api.OrganizationId.ToString("D"), refreshed,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_id_and_api_failures_are_generic_and_do_not_echo_input()
    {
        await using var factory = new EnabledSupportFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("https://support.showvault.test/")
        });
        client.DefaultRequestHeaders.Add("X-Support-Test-Auth", "1");
        var html = await client.GetStringAsync("/");
        var invalid = await client.PostAsync("/", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(html),
                ["OrganizationId"] = "not-an-organization-id"
            }));
        var invalidBody = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("overview is unavailable", invalidBody, StringComparison.Ordinal);
        Assert.DoesNotContain("not-an-organization-id", invalidBody, StringComparison.Ordinal);
        Assert.Equal(0, factory.Api.RequestCount);

        factory.Api.Fail = true;
        html = await client.GetStringAsync("/");
        var failed = await client.PostAsync("/", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(html),
                ["OrganizationId"] = factory.Api.OrganizationId.ToString("D")
            }));
        var failureBody = await failed.Content.ReadAsStringAsync();
        Assert.Contains("overview is unavailable", failureBody, StringComparison.Ordinal);
        Assert.DoesNotContain("provider-customer-fixture", failureBody, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.Api.OrganizationId.ToString("D"), failureBody,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cookie_contract_is_distinct_secure_host_only_and_server_side()
    {
        using var factory = new EnabledSupportFactory();
        var cookie = factory.Services.GetRequiredService<
            IOptionsMonitor<CookieAuthenticationOptions>>().Get(
                SupportAdminPortalOptions.CookieScheme);
        Assert.Equal("__Host-showvault-support", cookie.Cookie.Name);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite);
        Assert.Equal("/", cookie.Cookie.Path);
        Assert.Null(cookie.Cookie.Domain);
        Assert.False(cookie.SlidingExpiration);
        Assert.Equal(TimeSpan.FromMinutes(5), cookie.ExpireTimeSpan);
        Assert.IsType<SupportServerSideTicketStore>(cookie.SessionStore);
        var oidc = factory.Services.GetRequiredService<
            IOptionsMonitor<OpenIdConnectOptions>>().Get(SupportAdminPortalOptions.OidcScheme);
        Assert.Equal("__Host-showvault-support-nonce", oidc.NonceCookie.Name);
        Assert.Equal("__Host-showvault-support-correlation", oidc.CorrelationCookie.Name);
        Assert.Equal("/", oidc.NonceCookie.Path);
        Assert.Equal("/", oidc.CorrelationCookie.Path);
    }

    private static string AntiforgeryToken(string html)
    {
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static SupportAdminPortalOptions Complete() => new()
    {
        Enabled = true,
        Origin = "https://support.showvault.test/",
        ApiBaseUri = "https://support-api.showvault.test/",
        OidcAuthority = "https://support-identity.showvault.test/",
        OidcAudience = "https://support-api.showvault.test",
        OidcClientId = "support-fixture-client",
        OidcClientSecret = "runtime-only-support-fixture",
        SessionLifetimeMinutes = 5,
        ApiTimeoutSeconds = 15,
        MaximumApiResponseBytes = 262_144
    };

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class EnabledSupportFactory : WebApplicationFactory<Program>
    {
        public SyntheticSupportApiHandler Api { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            ConfigureComplete(builder);
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions,
                    SupportTestAuthenticationHandler>(SupportTestAuthenticationHandler.SchemeName,
                    _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                    options.DefaultAuthenticateScheme = SupportTestAuthenticationHandler.SchemeName);
                services.AddHttpClient<ShowVaultSupportClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => Api);
                services.PostConfigure<OpenIdConnectOptions>(
                    SupportAdminPortalOptions.OidcScheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = "https://support-identity.showvault.test/authorize",
                        TokenEndpoint = "https://support-identity.showvault.test/oauth/token",
                        Issuer = "https://support-identity.showvault.test/"
                    };
                    options.Configuration = configuration;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        }
    }

    private static void ConfigureComplete(IWebHostBuilder builder)
    {
        builder.UseSetting("SupportAdminPortal:Enabled", "true");
        builder.UseSetting("SupportAdminPortal:Origin", "https://support.showvault.test/");
        builder.UseSetting("SupportAdminPortal:ApiBaseUri", "https://support-api.showvault.test/");
        builder.UseSetting("SupportAdminPortal:OidcAuthority", "https://support-identity.showvault.test/");
        builder.UseSetting("SupportAdminPortal:OidcAudience", "https://support-api.showvault.test");
        builder.UseSetting("SupportAdminPortal:OidcClientId", "support-fixture-client");
        builder.UseSetting("SupportAdminPortal:OidcClientSecret", "runtime-only-support-fixture");
    }

    private sealed class SupportTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "SupportPortalTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Support-Test-Auth"))
                return Task.FromResult(AuthenticateResult.NoResult());
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken
            {
                Name = "access_token", Value = "support-access-token-fixture"
            }]);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "support|synthetic-reader")], SchemeName));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, properties, SchemeName)));
        }
    }

    public sealed class SyntheticSupportApiHandler : HttpMessageHandler
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public bool Fail { get; set; }
        public int RequestCount { get; private set; }
        public string? LastAuthorization { get; private set; }
        public string? LastMethod { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastAuthorization = request.Headers.Authorization?.ToString();
            LastMethod = request.Method.Method;
            LastPath = request.RequestUri!.AbsolutePath;
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (Fail)
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("provider-customer-fixture")
                };
            var members = new[] { "owner", "admin", "operator", "technician", "viewer" }
                .SelectMany(role => new[] { "active", "suspended", "removed" }
                    .Select(state => new SupportMemberCount(role, state,
                        role == "owner" && state == "active" ? 1 : 0))).ToArray();
            var overview = new SupportOrganizationOverview(OrganizationId,
                "Synthetic support organization", members,
                new("internal", "active", "active", null, null, true, "eligible", 1, 0, 100),
                new(0, Array.Empty<string>(), null),
                new([new("uploading", 1), new("completed", 2)], DateTimeOffset.UtcNow),
                new(null, null));
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(overview)
            };
            response.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            return response;
        }
    }
}
