using System.ComponentModel.DataAnnotations;

namespace ShowVault.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    [Required]
    public required Uri ControlPlaneUri { get; init; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    public string? EnrollmentCode { get; init; }

    public string? DataDirectory { get; init; }
}
