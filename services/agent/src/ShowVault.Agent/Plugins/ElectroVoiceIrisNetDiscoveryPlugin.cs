using System.IO.Compression;
using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class ElectroVoiceIrisNetDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.electro-voice-iris-net";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId, "ShowVault Electro-Voice IRIS-Net Project", "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.ElectroVoiceIrisNetProjectRoots;

    protected override string ProductName => "Electro-Voice IRIS-Net";

    protected override bool HasExpectedStructure(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(
                Path.GetExtension(path),
                ".ds",
                StringComparison.OrdinalIgnoreCase))
            .Any(IsIrisNetProjectArchive);

    private static bool IsIrisNetProjectArchive(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            return archive.Entries.Any(entry =>
                string.Equals(
                    entry.FullName.Replace('\\', '/'),
                    "main.ds",
                    StringComparison.OrdinalIgnoreCase) &&
                entry.Length > 0);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
