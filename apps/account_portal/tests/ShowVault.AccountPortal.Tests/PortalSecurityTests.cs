using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ShowVault.AccountPortal.Configuration;
using ShowVault.AccountPortal.Security;
using Xunit;

namespace ShowVault.AccountPortal.Tests;

public sealed class PortalSecurityTests
{
    [Fact]
    public async Task Checked_in_configuration_serves_only_disabled_response()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://account.showvault.test/")]
    [InlineData("https://user@example.test/")]
    [InlineData("https://example.test/path")]
    [InlineData("https://example.test/?query=1")]
    public void Origins_must_be_exact_https_roots(string value) =>
        Assert.False(AccountPortalOptions.HttpsRoot(value));

    [Fact]
    public void Production_cannot_enable_the_in_memory_ticket_implementation()
    {
        var options = Complete();
        Assert.True(options.IsComplete(development: true));
        Assert.False(options.IsComplete(development: false));
    }

    [Fact]
    public async Task Server_ticket_handle_is_opaque_and_one_time_secrets_clear()
    {
        var now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");
        var time = new FixedTimeProvider(now);
        var tickets = new ServerSideTicketStore(time);
        var properties = new AuthenticationProperties { ExpiresUtc = now.AddMinutes(30) };
        properties.StoreTokens([new AuthenticationToken
        {
            Name = "access_token",
            Value = "sensitive-token-fixture"
        }]);
        var ticket = new AuthenticationTicket(
            new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity("Test")), properties,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var handle = await tickets.StoreAsync(ticket);

        Assert.DoesNotContain("sensitive-token-fixture", handle, StringComparison.Ordinal);
        Assert.Equal(64, handle.Length);
        Assert.NotNull(await tickets.RetrieveAsync(handle));

        var secrets = new OneTimeSecretStore(time);
        var secretHandle = secrets.Put("invite-code-fixture");
        Assert.DoesNotContain("invite-code-fixture", secretHandle, StringComparison.Ordinal);
        Assert.Equal("invite-code-fixture", secrets.Take(secretHandle));
        Assert.Null(secrets.Take(secretHandle));
    }

    [Fact]
    public async Task Ephemeral_stores_evict_at_their_fixed_capacity()
    {
        var now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");
        var time = new FixedTimeProvider(now);
        var properties = new AuthenticationProperties { ExpiresUtc = now.AddMinutes(30) };
        var ticket = new AuthenticationTicket(
            new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity("Test")), properties,
            CookieAuthenticationDefaults.AuthenticationScheme);
        var tickets = new ServerSideTicketStore(time);
        var handles = new List<string>();
        for (var index = 0; index < 4100; index++)
            handles.Add(await tickets.StoreAsync(ticket));
        var retainedTickets = 0;
        foreach (var handle in handles)
            if (await tickets.RetrieveAsync(handle) is not null) retainedTickets++;
        Assert.Equal(4096, retainedTickets);

        var secrets = new OneTimeSecretStore(time);
        var secretHandles = Enumerable.Range(0, 1030)
            .Select(index => secrets.Put(index.ToString())).ToArray();
        Assert.Equal(1024, secretHandles.Count(handle => secrets.Take(handle) is not null));
    }

    [Fact]
    public async Task Enabled_index_uses_secure_cookie_contract_and_antiforgery()
    {
        await using var factory = new EnabledPortalFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://account.showvault.test/")
        });
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("access_token", html, StringComparison.OrdinalIgnoreCase);

        var missingCsrf = await client.PostAsync("/", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        var cookie = factory.Services.GetRequiredService<
            IOptionsMonitor<CookieAuthenticationOptions>>().Get(
                CookieAuthenticationDefaults.AuthenticationScheme).Cookie;
        Assert.Equal("__Host-showvault-account", cookie.Name);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal("/", cookie.Path);
        Assert.Null(cookie.Domain);
    }

    [Fact]
    public async Task Enabled_portal_enforces_origin_and_sends_oauth_audience_with_pkce()
    {
        await using var factory = new EnabledPortalFactory();
        using var wrongOrigin = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://evil.showvault.test/")
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await wrongOrigin.GetAsync("/")).StatusCode);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://account.showvault.test/")
        });
        var challenge = await client.GetAsync("/Organizations/Select");

        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var location = Assert.IsType<Uri>(challenge.Headers.Location);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("https://api.showvault.test", query["audience"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrWhiteSpace(query["code_challenge"]));
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
        Assert.False(string.IsNullOrWhiteSpace(query["nonce"]));
    }

    [Fact]
    public async Task Step_up_redirect_event_preserves_audience_and_requests_fresh_mfa()
    {
        await using var factory = new EnabledPortalFactory();
        var options = factory.Services.GetRequiredService<
            IOptionsMonitor<OpenIdConnectOptions>>().Get(
                OpenIdConnectDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties();
        properties.Items["showvault_step_up"] = "1";
        var redirect = new RedirectContext(new DefaultHttpContext(),
            new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme,
                typeof(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectHandler)),
            options, properties)
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        await options.Events.RedirectToIdentityProvider(redirect);

        Assert.Equal("https://api.showvault.test",
            redirect.ProtocolMessage.GetParameter("audience"));
        Assert.Equal("openid profile manage:members", redirect.ProtocolMessage.Scope);
        Assert.Equal("http://schemas.openid.net/pape/policies/2007/06/multi-factor",
            redirect.ProtocolMessage.AcrValues);
        Assert.Equal("0", redirect.ProtocolMessage.MaxAge);
    }

    [Fact]
    public async Task Unhandled_failures_return_only_generic_problem_details()
    {
        await using var factory = new EnabledPortalFactory(configureOidc: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://account.showvault.test/")
        });

        var response = await client.GetAsync("/Organizations/Select");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("could not complete the request", body, StringComparison.Ordinal);
        Assert.DoesNotContain("IDX", body, StringComparison.Ordinal);
        Assert.DoesNotContain("identity.showvault.test", body, StringComparison.Ordinal);
    }

    private static AccountPortalOptions Complete() => new()
    {
        Enabled = true,
        Origin = "https://account.showvault.test/",
        ApiBaseUri = "https://api.showvault.test/",
        Auth0Authority = "https://identity.showvault.test/",
        Auth0Audience = "https://api.showvault.test",
        Auth0ClientId = "fixture-client",
        Auth0ClientSecret = "runtime-only-fixture",
        SessionLifetimeMinutes = 30,
        ApiTimeoutSeconds = 15,
        MaximumApiResponseBytes = 1_048_576
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class EnabledPortalFactory(bool configureOidc = true)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("AccountPortal:Enabled", "true");
            builder.UseSetting("AccountPortal:Origin", "https://account.showvault.test/");
            builder.UseSetting("AccountPortal:ApiBaseUri", "https://api.showvault.test/");
            builder.UseSetting("AccountPortal:Auth0Authority", "https://identity.showvault.test/");
            builder.UseSetting("AccountPortal:Auth0Audience", "https://api.showvault.test");
            builder.UseSetting("AccountPortal:Auth0ClientId", "fixture-client");
            builder.UseSetting("AccountPortal:Auth0ClientSecret", "runtime-only-fixture");
            if (configureOidc)
                builder.ConfigureServices(services => services.PostConfigure<OpenIdConnectOptions>(
                    OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = "https://identity.showvault.test/authorize",
                        TokenEndpoint = "https://identity.showvault.test/oauth/token",
                        Issuer = "https://identity.showvault.test/"
                    };
                    options.Configuration = configuration;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                }));
        }
    }
}
