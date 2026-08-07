using System.Security.Cryptography;
using ShowVault.Platform.Agents;
using Xunit;

namespace ShowVault.Platform.Tests;

public sealed class AgentIdentityTests
{
    [Fact]
    public void Enrollment_is_single_use_and_expires()
    {
        var now = DateTimeOffset.UtcNow;
        var enrollment = AgentEnrollment.Create(
            Guid.NewGuid(),
            RandomNumberGenerator.GetBytes(32),
            "auth0|owner",
            now,
            TimeSpan.FromMinutes(15));

        Assert.True(enrollment.CanBeConsumed(now.AddMinutes(14)));
        Assert.False(enrollment.CanBeConsumed(now.AddMinutes(15)));

        enrollment.Consume(now.AddMinutes(1));
        Assert.False(enrollment.CanBeConsumed(now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => enrollment.Consume(now.AddMinutes(2)));
    }

    [Fact]
    public void Revoked_agent_records_revocation_once()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var agent = VenueAgent.Create(
            Guid.NewGuid(),
            "Main Control Agent",
            RandomNumberGenerator.GetBytes(32),
            createdAt);
        var revokedAt = createdAt.AddMinutes(1);

        agent.Revoke(revokedAt);
        agent.Revoke(revokedAt.AddMinutes(1));

        Assert.Equal(revokedAt, agent.RevokedAt);
    }
}
