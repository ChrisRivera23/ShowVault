using System.Xml;
using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class BlackmagicAtemDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : ExactRootFileDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.blackmagic-atem";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Blackmagic ATEM Switcher State",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.BlackmagicAtemStateRoots;

    protected override string ProductName => "Blackmagic ATEM";

    protected override bool HasExpectedStructure(string rootPath) =>
        Directory.EnumerateFiles(
            rootPath,
            "*",
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            })
            .Where(path => string.Equals(
                Path.GetExtension(path),
                ".xml",
                StringComparison.OrdinalIgnoreCase))
            .Any(IsAtemProfile);

    private static bool IsAtemProfile(string path)
    {
        try
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            reader.MoveToContent();
            return string.Equals(reader.LocalName, "Profile", StringComparison.Ordinal) &&
                reader.GetAttribute("product") is { } product &&
                product.StartsWith("ATEM ", StringComparison.OrdinalIgnoreCase);
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
