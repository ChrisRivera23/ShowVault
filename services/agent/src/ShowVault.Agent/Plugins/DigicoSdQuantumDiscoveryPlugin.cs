using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class DigicoSdQuantumDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.digico-sd-quantum";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault DiGiCo SD/Quantum Session",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.DigicoSdQuantumSessionRoots;

    protected override string ProductName => "DiGiCo SD/Quantum";

    protected override bool HasExpectedStructure(string rootPath) =>
        ContainsExtension(rootPath, ".ses");
}
