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

    public IReadOnlyList<string> NetworkDiscoveryTargets { get; init; } = [];

    public IReadOnlyList<string> ResolumeDiscoveryRoots { get; init; } = [];

    public IReadOnlyList<string> ResolumeUserDataRoots { get; init; } = [];

    public IReadOnlyList<string> GrandMa2ExportRoots { get; init; } = [];

    public IReadOnlyList<string> GrandMa3ExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaDm7ExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaRivageExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaClQlExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaTfExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaDm3ExportRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaDme7ProjectRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaMtxMrxProjectRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaPcDdiProjectRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaProVisionaireControlProjectRoots { get; init; } = [];

    public IReadOnlyList<string> YamahaDme5Dme3ProjectRoots { get; init; } = [];

    public IReadOnlyList<string> QsysDesignerProjectRoots { get; init; } = [];

    public IReadOnlyList<string> EtcEosShowArchiveRoots { get; init; } = [];
}
