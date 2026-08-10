using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ShowVault.Api.Security;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class PersonalBetaAuthenticationTests
{
    [Fact]
    public void RecognizesOnlyTheExactPersonalBetaAuthorizationValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer showvault-personal-beta-loopback";

        Assert.True(PersonalBetaAuthenticationHandler.IsPersonalBetaRequest(context.Request));

        context.Request.Headers.Authorization = "Bearer showvault-personal-beta-loopback-extra";
        Assert.False(PersonalBetaAuthenticationHandler.IsPersonalBetaRequest(context.Request));
    }

    [Theory]
    [InlineData("Production", true, "auth0|owner", "127.0.0.1", false)]
    [InlineData("Development", false, "auth0|owner", "127.0.0.1", false)]
    [InlineData("Development", true, "", "127.0.0.1", false)]
    [InlineData("Development", true, "auth0|owner", "192.0.2.10", false)]
    [InlineData("Development", true, "auth0|owner", "127.0.0.1", true)]
    [InlineData("Development", true, "auth0|owner", "::1", true)]
    public void PersonalBetaRequiresEveryDevelopmentAndLoopbackGuard(
        string environmentName,
        bool enabled,
        string subject,
        string remoteAddress,
        bool expected)
    {
        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = environmentName
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PersonalBeta:BypassAuthentication"] = enabled.ToString(),
                ["PersonalBeta:IdentitySubject"] = subject
            })
            .Build();

        var result = PersonalBetaAuthenticationHandler.TryGetIdentitySubject(
            environment,
            configuration,
            IPAddress.Parse(remoteAddress),
            out var resolvedSubject);

        Assert.Equal(expected, result);
        if (expected) Assert.Equal(subject, resolvedSubject);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ShowVault.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
