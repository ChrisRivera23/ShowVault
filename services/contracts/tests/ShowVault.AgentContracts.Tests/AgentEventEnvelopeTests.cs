using ShowVault.AgentContracts;
using Xunit;

namespace ShowVault.AgentContracts.Tests;

public sealed class AgentEventEnvelopeTests
{
    [Fact]
    public void Valid_event_passes_shared_validation()
    {
        var envelope = AgentEventEnvelope.Create(
            Guid.NewGuid(),
            AgentEventType.AgentConnected,
            "correlation-1",
            "{\"status\":\"connected\"}",
            DateTimeOffset.UtcNow);

        Assert.True(AgentEventValidation.TryValidate(envelope, out var error));
        Assert.Empty(error);
    }

    [Fact]
    public void Unsupported_event_type_is_rejected()
    {
        var envelope = ValidEvent() with { Type = (AgentEventType)999 };

        Assert.False(AgentEventValidation.TryValidate(envelope, out var error));
        Assert.Equal("Event type is unsupported.", error);
    }

    [Fact]
    public void Unsupported_protocol_is_rejected()
    {
        var envelope = ValidEvent() with { ProtocolVersion = "unsupported" };

        Assert.False(AgentEventValidation.TryValidate(envelope, out var error));
        Assert.Equal("Protocol version is unsupported.", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_correlation_id_is_rejected(string correlationId)
    {
        var envelope = ValidEvent() with { CorrelationId = correlationId };

        Assert.False(AgentEventValidation.TryValidate(envelope, out _));
    }

    [Fact]
    public void Oversized_correlation_id_is_rejected()
    {
        var envelope = ValidEvent() with
        {
            CorrelationId = new string('a', AgentEventValidation.MaxCorrelationIdLength + 1)
        };

        Assert.False(AgentEventValidation.TryValidate(envelope, out _));
    }

    [Fact]
    public void Invalid_json_payload_is_rejected()
    {
        var envelope = ValidEvent() with { Payload = "not-json" };

        Assert.False(AgentEventValidation.TryValidate(envelope, out var error));
        Assert.Equal("Payload must be valid JSON with a maximum depth of 32.", error);
    }

    [Fact]
    public void Oversized_payload_is_rejected()
    {
        var envelope = ValidEvent() with
        {
            Payload = $"\"{new string('a', AgentEventValidation.MaxPayloadUtf8Bytes)}\""
        };

        Assert.False(AgentEventValidation.TryValidate(envelope, out _));
    }

    private static AgentEventEnvelope ValidEvent() => AgentEventEnvelope.Create(
        Guid.NewGuid(),
        AgentEventType.AgentConnected,
        "correlation-1",
        "{}",
        DateTimeOffset.UtcNow);
}
