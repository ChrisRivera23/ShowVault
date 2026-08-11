using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Identity;
using ShowVault.AgentContracts;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class AgentIdentityBootstrapperTests
{
    [Fact]
    public void Credential_states_round_trip_through_secure_store_serialization()
    {
        var identity = NewIdentity("agent.sva_active");
        StoredAgentState[] states =
        [
            new ActiveAgentState(identity),
            new PendingAgentEnrollment(
                Guid.NewGuid(),
                "sve_pending",
                "Test Agent",
                $"sva_{new string('a', 64)}"),
            new PendingAgentRotation(
                Guid.NewGuid(),
                identity,
                $"sva_{new string('b', 64)}")
        ];

        foreach (var state in states)
        {
            var serialized = AgentCredentialSerialization.Serialize(state);
            Assert.Equal(state, AgentCredentialSerialization.Deserialize(serialized));
        }
    }

    [Fact]
    public async Task Existing_identity_is_loaded_without_enrollment()
    {
        var identity = NewIdentity("credential");
        var store = new MemoryCredentialStore(new ActiveAgentState(identity));
        var handler = new RecordingHandler((_, _) =>
            throw new InvalidOperationException("Unexpected request."));
        var bootstrapper = CreateBootstrapper(store, handler, enrollmentCode: null);

        var result = await bootstrapper.GetOrEnrollAsync(CancellationToken.None);

        Assert.Equal(identity, result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task First_start_persists_pending_state_before_enrollment()
    {
        var agentId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var handler = EnrollmentHandler(agentId, venueId);
        var store = new MemoryCredentialStore();
        var bootstrapper = CreateBootstrapper(store, handler, "sve_one_time_code");

        var result = await bootstrapper.GetOrEnrollAsync(CancellationToken.None);
        var restartedResult = await bootstrapper.GetOrEnrollAsync(CancellationToken.None);

        Assert.Equal(agentId, result.AgentId);
        Assert.Equal(venueId, result.VenueId);
        Assert.Equal(result, restartedResult);
        Assert.Equal(2, store.SuccessfulSaveCount);
        Assert.Equal(1, handler.RequestCount);
        Assert.IsType<ActiveAgentState>(store.State);
    }

    [Fact]
    public async Task Missing_identity_and_enrollment_code_fails_closed()
    {
        var bootstrapper = CreateBootstrapper(
            new MemoryCredentialStore(),
            new RecordingHandler((_, _) =>
                throw new InvalidOperationException("Unexpected request.")),
            enrollmentCode: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrapper.GetOrEnrollAsync(CancellationToken.None));

        Assert.Contains("not enrolled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rotation_replaces_the_stored_credential_after_server_success()
    {
        var identity = NewIdentity("old-credential");
        var handler = RotationHandler(identity);
        var store = new MemoryCredentialStore(new ActiveAgentState(identity));
        var bootstrapper = CreateBootstrapper(store, handler, enrollmentCode: null);

        var result = await bootstrapper.RotateCredentialAsync(identity, CancellationToken.None);

        Assert.NotEqual(identity.Credential, result.Credential);
        Assert.Equal(2, store.SuccessfulSaveCount);
        Assert.Equal(new ActiveAgentState(result), store.State);
    }

    [Fact]
    public async Task Enrollment_resumes_after_server_success_and_active_save_failure()
    {
        var agentId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var handler = EnrollmentHandler(agentId, venueId);
        var store = new MemoryCredentialStore { ActiveSaveFailuresRemaining = 1 };
        var bootstrapper = CreateBootstrapper(store, handler, "sve_recoverable_code");

        await Assert.ThrowsAsync<IOException>(() =>
            bootstrapper.GetOrEnrollAsync(CancellationToken.None));
        var pending = Assert.IsType<PendingAgentEnrollment>(store.State);

        var restarted = CreateBootstrapper(store, handler, enrollmentCode: null);
        var recovered = await restarted.GetOrEnrollAsync(CancellationToken.None);

        Assert.Equal(agentId, recovered.AgentId);
        Assert.Equal(venueId, recovered.VenueId);
        Assert.EndsWith(pending.CredentialSecret, recovered.Credential, StringComparison.Ordinal);
        Assert.Equal(new ActiveAgentState(recovered), store.State);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Rotation_resumes_with_new_credential_after_active_save_failure()
    {
        var identity = NewIdentity("old-credential");
        var handler = RotationHandler(identity);
        var store = new MemoryCredentialStore(new ActiveAgentState(identity))
        {
            ActiveSaveFailuresRemaining = 1
        };
        var bootstrapper = CreateBootstrapper(store, handler, enrollmentCode: null);

        await Assert.ThrowsAsync<IOException>(() =>
            bootstrapper.RotateCredentialAsync(identity, CancellationToken.None));
        var pending = Assert.IsType<PendingAgentRotation>(store.State);

        var restarted = CreateBootstrapper(store, handler, enrollmentCode: null);
        var recovered = await restarted.GetOrEnrollAsync(CancellationToken.None);

        Assert.Equal(identity.AgentId, recovered.AgentId);
        Assert.EndsWith(pending.CredentialSecret, recovered.Credential, StringComparison.Ordinal);
        Assert.Equal(new ActiveAgentState(recovered), store.State);
        Assert.Equal(3, handler.RequestCount);
    }

    private static RecordingHandler EnrollmentHandler(Guid agentId, Guid venueId)
    {
        Guid? requestId = null;
        string? credentialSecret = null;
        return new RecordingHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "https://control.showvault.test/api/v1/agents/enroll",
                request.RequestUri?.ToString());
            var body = await request.Content!.ReadFromJsonAsync<EnrollAgentRequest>(
                cancellationToken);
            Assert.NotNull(body);
            requestId ??= body.RequestId;
            credentialSecret ??= body.CredentialSecret;
            Assert.Equal(requestId, body.RequestId);
            Assert.Equal(credentialSecret, body.CredentialSecret);
            return JsonResponse(new
            {
                payload = new
                {
                    agentId,
                    venueId,
                    credential = $"{agentId}.{body.CredentialSecret}"
                }
            });
        });
    }

    private static RecordingHandler RotationHandler(StoredAgentIdentity identity)
    {
        var serverCredential = identity.Credential;
        Guid? completedRequestId = null;
        return new RecordingHandler(async (request, cancellationToken) =>
        {
            var authentication = request.Headers.Authorization?.Parameter;
            if (!string.Equals(authentication, serverCredential, StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            var body = await request.Content!.ReadFromJsonAsync<RotateAgentCredentialRequest>(
                cancellationToken);
            Assert.NotNull(body);
            if (completedRequestId is null)
            {
                completedRequestId = body.RequestId;
                serverCredential = $"{identity.AgentId}.{body.CredentialSecret}";
            }
            else
            {
                Assert.Equal(completedRequestId, body.RequestId);
                Assert.Equal(serverCredential, $"{identity.AgentId}.{body.CredentialSecret}");
            }

            return JsonResponse(new
            {
                payload = new
                {
                    credential = serverCredential,
                    rotatedAt = DateTimeOffset.UtcNow
                }
            });
        });
    }

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value)
    };

    private static StoredAgentIdentity NewIdentity(string credential) =>
        new(Guid.NewGuid(), Guid.NewGuid(), credential);

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

    private sealed class MemoryCredentialStore(StoredAgentState? state = null)
        : IAgentCredentialStore
    {
        public StoredAgentState? State { get; private set; } = state;
        public int SuccessfulSaveCount { get; private set; }
        public int ActiveSaveFailuresRemaining { get; set; }

        public ValueTask<StoredAgentState?> LoadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(State);

        public ValueTask SaveAsync(
            StoredAgentState state,
            CancellationToken cancellationToken)
        {
            if (state is ActiveAgentState && ActiveSaveFailuresRemaining > 0)
            {
                ActiveSaveFailuresRemaining--;
                throw new IOException("Synthetic credential-store failure.");
            }

            State = state;
            SuccessfulSaveCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(CancellationToken cancellationToken)
        {
            State = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return responseFactory(request, cancellationToken);
        }
    }
}
