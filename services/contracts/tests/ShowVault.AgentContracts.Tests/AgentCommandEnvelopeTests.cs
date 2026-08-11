using ShowVault.AgentContracts;
using Xunit;

namespace ShowVault.AgentContracts.Tests;

public sealed class AgentCommandEnvelopeTests
{
    [Fact]
    public void Valid_command_passes_shared_validation()
    {
        var envelope = ValidCommand();

        Assert.True(AgentCommandValidation.TryValidate(envelope, out var error));
        Assert.Empty(error);
    }

    [Fact]
    public void Unsupported_command_type_is_rejected()
    {
        var envelope = ValidCommand() with { Type = (AgentCommandType)999 };

        Assert.False(AgentCommandValidation.TryValidate(envelope, out var error));
        Assert.Equal("Command type is unsupported.", error);
    }

    [Fact]
    public void Unsupported_protocol_is_rejected()
    {
        var envelope = ValidCommand() with { ProtocolVersion = "unsupported" };

        Assert.False(AgentCommandValidation.TryValidate(envelope, out var error));
        Assert.Equal("Protocol version is unsupported.", error);
    }

    [Fact]
    public void Empty_identifiers_are_rejected()
    {
        Assert.False(AgentCommandValidation.TryValidate(
            ValidCommand() with { CommandId = Guid.Empty },
            out _));
        Assert.False(AgentCommandValidation.TryValidate(
            ValidCommand() with { AgentId = Guid.Empty },
            out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_correlation_id_is_rejected(string correlationId)
    {
        Assert.False(AgentCommandValidation.TryValidate(
            ValidCommand() with { CorrelationId = correlationId },
            out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void Invalid_validity_is_rejected(int validForSeconds)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var envelope = ValidCommand() with
        {
            IssuedAt = issuedAt,
            ExpiresAt = issuedAt.AddSeconds(validForSeconds)
        };

        Assert.False(AgentCommandValidation.TryValidate(envelope, out _));
    }

    [Fact]
    public void Oversized_correlation_id_is_rejected()
    {
        var envelope = ValidCommand() with
        {
            CorrelationId = new string(
                'a',
                AgentCommandValidation.MaxCorrelationIdLength + 1)
        };

        Assert.False(AgentCommandValidation.TryValidate(envelope, out _));
    }

    [Fact]
    public void Invalid_json_payload_is_rejected()
    {
        var envelope = ValidCommand() with { Payload = "not-json" };

        Assert.False(AgentCommandValidation.TryValidate(envelope, out var error));
        Assert.Equal("Payload must be valid JSON with a maximum depth of 32.", error);
    }

    [Fact]
    public void Null_payload_is_rejected()
    {
        var envelope = ValidCommand() with { Payload = null! };

        Assert.False(AgentCommandValidation.TryValidate(envelope, out _));
    }

    [Fact]
    public void Oversized_utf8_payload_is_rejected()
    {
        var envelope = ValidCommand() with
        {
            Payload = $"\"{new string('é', AgentCommandValidation.MaxPayloadUtf8Bytes / 2)}\""
        };

        Assert.False(AgentCommandValidation.TryValidate(envelope, out _));
    }

    [Fact]
    public void Excessively_deep_payload_is_rejected()
    {
        var envelope = ValidCommand() with
        {
            Payload = $"{new string('[', 33)}0{new string(']', 33)}"
        };

        Assert.False(AgentCommandValidation.TryValidate(envelope, out _));
    }

    private static AgentCommandEnvelope ValidCommand() => AgentCommandEnvelope.Create(
        Guid.NewGuid(),
        AgentCommandType.StartDiscovery,
        "correlation-1",
        "{}",
        DateTimeOffset.UtcNow,
        TimeSpan.FromMinutes(5));
}
