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

    public string? PackageDirectory { get; init; }

    public IReadOnlyList<string> DiscoveryRoots { get; init; } = [];

    public IReadOnlyList<string> RestoreRoots { get; init; } = [];

    public IReadOnlyList<string> ResolumeDiscoveryRoots { get; init; } = [];

    public IReadOnlyList<string> ResolumeUserDataRoots { get; init; } = [];

    public IReadOnlyList<string> GrandMa2ShowExportRoots { get; init; } = [];

    public IReadOnlyList<string> GrandMa3ShowExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaDm7SettingsExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaRivageSettingsExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaClQlSettingsExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaTfSettingsExportRoots { get; init; } = [];
}
