using System.ComponentModel.DataAnnotations;

namespace ShowVault.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    [Required]
    public Guid AgentId { get; init; }

    [Required]
    public required Uri ControlPlaneUri { get; init; }
}
