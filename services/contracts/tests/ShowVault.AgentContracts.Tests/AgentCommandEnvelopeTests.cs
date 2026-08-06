using Xunit;

namespace ShowVault.AgentContracts.Tests;

public sealed class AgentCommandEnvelopeTests
{
    [Fact]
    public void Create_builds_a_versioned_time_bounded_command()
    {
        var command = AgentCommandEnvelope.Create(
            Guid.NewGuid(),
            AgentCommandType.StartDiscovery,
            "correlation-1",
            "{}",
            TimeSpan.FromMinutes(5));

        Assert.Equal(AgentProtocol.Version, command.ProtocolVersion);
        Assert.True(command.ExpiresAt > command.IssuedAt);
        Assert.NotEqual(Guid.Empty, command.CommandId);
    }

    [Fact]
    public void Create_rejects_an_empty_agent_identifier()
    {
        Assert.Throws<ArgumentException>(() => AgentCommandEnvelope.Create(
            Guid.Empty,
            AgentCommandType.StartDiscovery,
            "correlation-1",
            "{}",
            TimeSpan.FromMinutes(5)));
    }
}
