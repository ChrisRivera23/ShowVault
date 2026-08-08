using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class DasAudioAlmaDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.das-audio-alma";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault DAS Audio ALMA Project", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.DasAudioAlmaDataRoots;

    protected override string ProductName => "DAS Audio ALMA";

    protected override bool HasExpectedStructure(string rootPath)
    {
        var projectDirectory = Path.Combine(rootPath, "prj");
        if (!Directory.Exists(projectDirectory))
        {
            return false;
        }

        return Directory.EnumerateFiles(projectDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(Path.GetExtension(path), ".prj", StringComparison.OrdinalIgnoreCase))
            .Any(IsAlmaProject);
    }

    private static bool IsAlmaProject(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("name", out var name) &&
                   name.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrWhiteSpace(name.GetString()) &&
                   root.TryGetProperty("guid", out var guid) &&
                   guid.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrWhiteSpace(guid.GetString()) &&
                   root.TryGetProperty("version", out _) &&
                   root.TryGetProperty("zones", out var zones) &&
                   zones.ValueKind == JsonValueKind.Array &&
                   root.TryGetProperty("snapshots", out var snapshots) &&
                   snapshots.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
