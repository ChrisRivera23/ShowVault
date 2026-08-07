using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Identity;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class AgentIdentityBootstrapperTests
{
    [Fact]
    public async Task Existing_identity_is_loaded_without_enrollment()
    {
        var identity = new StoredAgentIdentity(Guid.NewGuid(), Guid.NewGuid(), "credential");
        var store = new MemoryCredentialStore(identity);
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Unexpected request."));
        var bootstrapper = CreateBootstrapper(store, handler, enrollmentCode: null);

        var result = await bootstrapper.GetOrEnrollAsync(CancellationToken.None);

        Assert.Equal(identity, result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task First_start_enrolls_and_saves_identity_once()
    {
        var agentId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        const string credential = "agent-id.sva_secret";
        var handler = new RecordingHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://control.showvault.test/api/v1/agents/enroll", request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    payload = new { agentId, venueId, credential }
                })
            };
        });
        var store = new MemoryCredentialStore();
        var bootstrapper = CreateBootstrapper(store, handler, "sve_one_time_code");

        var result = await bootstrapper.GetOrEnrollAsync(CancellationToken.None);
        var restartedResult = await bootstrapper.GetOrEnrollAsync(CancellationToken.None);

        Assert.Equal(new StoredAgentIdentity(agentId, venueId, credential), result);
        Assert.Equal(result, restartedResult);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Missing_identity_and_enrollment_code_fails_closed()
    {
        var bootstrapper = CreateBootstrapper(
            new MemoryCredentialStore(),
            new RecordingHandler(_ => throw new InvalidOperationException("Unexpected request.")),
            enrollmentCode: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrapper.GetOrEnrollAsync(CancellationToken.None));

        Assert.Contains("not enrolled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rotation_replaces_the_stored_credential_after_server_success()
    {
        var identity = new StoredAgentIdentity(Guid.NewGuid(), Guid.NewGuid(), "old-credential");
        const string rotatedCredential = "agent-id.sva_rotated";
        var handler = new RecordingHandler(request =>
        {
            Assert.Equal("ShowVault-Agent", request.Headers.Authorization?.Scheme);
            Assert.Equal(identity.Credential, request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    payload = new
                    {
                        credential = rotatedCredential,
                        rotatedAt = DateTimeOffset.UtcNow
                    }
                })
            };
        });
        var store = new MemoryCredentialStore(identity);
        var bootstrapper = CreateBootstrapper(store, handler, enrollmentCode: null);

        var result = await bootstrapper.RotateCredentialAsync(identity, CancellationToken.None);

        Assert.Equal(rotatedCredential, result.Credential);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(result, await store.LoadAsync(CancellationToken.None));
    }

    private static AgentIdentityBootstrapper CreateBootstrapper(
        IAgentCredentialStore store,
        HttpMessageHandler handler,
        string? enrollmentCode)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://control.showvault.test")
        };
        var options = Options.Create(new AgentOptions
        {
            ControlPlaneUri = client.BaseAddress,
            Name = "Test Agent",
            EnrollmentCode = enrollmentCode
        });
        return new AgentIdentityBootstrapper(store, new AgentEnrollmentClient(client), options);
    }

    private sealed class MemoryCredentialStore(StoredAgentIdentity? identity = null)
        : IAgentCredentialStore
    {
        private StoredAgentIdentity? _identity = identity;

        public int SaveCount { get; private set; }

        public ValueTask<StoredAgentIdentity?> LoadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(_identity);

        public ValueTask SaveAsync(
            StoredAgentIdentity identity,
            CancellationToken cancellationToken)
        {
            _identity = identity;
            SaveCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(CancellationToken cancellationToken)
        {
            _identity = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
