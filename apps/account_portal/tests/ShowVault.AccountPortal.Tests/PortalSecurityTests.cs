using System.Net;
using System.Net.Http.Json;
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
using ShowVault.AccountPortal.Configuration;
using ShowVault.AccountPortal.Clients;
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

    [Fact]
    public async Task Authenticated_pages_keep_tokens_and_subjects_server_side_and_reveal_code_once()
    {
        await using var factory = new EnabledPortalFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://account.showvault.test/")
        });
        client.DefaultRequestHeaders.Add("X-Portal-Test-Auth", "1");
        var pagePath = $"/Organizations/Members/{factory.Api.OrganizationId}";
        var page = await client.GetAsync(pagePath);
        var html = await page.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Synthetic organization members", html, StringComparison.Ordinal);
        Assert.Contains("Synthetic member", html, StringComparison.Ordinal);
        Assert.DoesNotContain("auth0|portal-test", html, StringComparison.Ordinal);
        Assert.DoesNotContain("portal-access-token", html, StringComparison.Ordinal);
        var token = AntiforgeryToken(html);

        var create = await client.PostAsync(pagePath + "?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["organizationId"] = factory.Api.OrganizationId.ToString(),
                ["displayLabel"] = "Browser invite",
                ["role"] = "viewer"
            }));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        Assert.Contains("reveal=", create.Headers.Location!.OriginalString,
            StringComparison.Ordinal);
        Assert.Equal("Bearer portal-access-token", factory.Api.LastAuthorization);

        var reveal = await client.GetAsync(create.Headers.Location);
        var revealHtml = await reveal.Content.ReadAsStringAsync();
        Assert.Contains("one-time-invitation-code", revealHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("auth0|portal-test", revealHtml, StringComparison.Ordinal);
        var refreshed = await client.GetStringAsync(create.Headers.Location);
        Assert.DoesNotContain("one-time-invitation-code", refreshed, StringComparison.Ordinal);

        var mutate = await client.PostAsync(pagePath + "?handler=Mutate",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(revealHtml),
                ["organizationId"] = factory.Api.OrganizationId.ToString(),
                ["membershipId"] = factory.Api.MembershipId.ToString(),
                ["action"] = "suspend",
                ["revision"] = "1"
            }));
        Assert.Equal(HttpStatusCode.Redirect, mutate.StatusCode);
        Assert.Equal("PATCH", factory.Api.LastMethod);
        Assert.Contains("\"action\":\"suspend\"", factory.Api.LastBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Typed_api_forbidden_maps_to_explicit_step_up_redirect()
    {
        await using var factory = new EnabledPortalFactory();
        factory.Api.ForbidMutation = true;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://account.showvault.test/")
        });
        client.DefaultRequestHeaders.Add("X-Portal-Test-Auth", "1");
        var pagePath = $"/Organizations/Members/{factory.Api.OrganizationId}";
        var html = await client.GetStringAsync(pagePath);

        var response = await client.PostAsync(pagePath + "?handler=Mutate",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(html),
                ["organizationId"] = factory.Api.OrganizationId.ToString(),
                ["membershipId"] = factory.Api.MembershipId.ToString(),
                ["action"] = "suspend",
                ["revision"] = "1"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/StepUp", response.Headers.Location!.OriginalString,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invitation_failure_message_is_rendered_without_echoing_a_code()
    {
        await using var factory = new EnabledPortalFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://account.showvault.test/")
        });
        client.DefaultRequestHeaders.Add("X-Portal-Test-Auth", "1");

        var html = await client.GetStringAsync("/Invitations/Accept?unavailable=true");

        Assert.Contains("The invitation is unavailable.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("one-time-invitation-code", html, StringComparison.Ordinal);
        Assert.DoesNotContain("portal-access-token", html, StringComparison.Ordinal);
    }

    private static string AntiforgeryToken(string html)
    {
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
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
        public SyntheticAccountApiHandler Api { get; } = new();

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
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions,
                    PortalTestAuthenticationHandler>(
                    PortalTestAuthenticationHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                    options.DefaultAuthenticateScheme =
                        PortalTestAuthenticationHandler.SchemeName);
                services.AddHttpClient<ShowVaultAccountClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => Api);
            });
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

    private sealed class PortalTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PortalTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Portal-Test-Auth"))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "auth0|portal-test")], SchemeName));
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken
            {
                Name = "access_token",
                Value = "portal-access-token"
            }]);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, properties, SchemeName)));
        }
    }

    public sealed class SyntheticAccountApiHandler : HttpMessageHandler
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public Guid MembershipId { get; } = Guid.NewGuid();
        public bool ForbidMutation { get; set; }
        public string? LastAuthorization { get; private set; }
        public string? LastMethod { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastAuthorization = request.Headers.Authorization?.ToString();
            LastMethod = request.Method.Method;
            LastBody = request.Content is null ? null :
                await request.Content.ReadAsStringAsync(cancellationToken);
            if (ForbidMutation && request.Method != HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            object payload = request.RequestUri!.AbsolutePath switch
            {
                "/api/v1/organizations" => new[]
                {
                    new OrganizationView(OrganizationId, "Synthetic organization",
                        "synthetic", "owner")
                },
                var path when path.EndsWith("/account/members", StringComparison.Ordinal) =>
                    new[]
                    {
                        new MemberView(MembershipId, "Synthetic member", "viewer", "active",
                            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1)
                    },
                var path when path.EndsWith("/account/invitations", StringComparison.Ordinal) &&
                    request.Method == HttpMethod.Get => Array.Empty<InvitationView>(),
                var path when path.EndsWith("/account/invitations", StringComparison.Ordinal) =>
                    new CreatedInvitationView(Guid.NewGuid(), "Browser invite", "viewer",
                        "pending", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow.AddDays(7), 1, "one-time-invitation-code"),
                var path when path.Contains("/account/members/", StringComparison.Ordinal) =>
                    new MemberView(MembershipId, "Synthetic member", "viewer", "suspended",
                        false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 2),
                _ => throw new InvalidOperationException("Unexpected synthetic API request.")
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { payload })
            };
        }
    }
}
