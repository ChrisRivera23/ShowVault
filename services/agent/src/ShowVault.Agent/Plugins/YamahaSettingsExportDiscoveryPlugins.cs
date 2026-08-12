using Microsoft.Extensions.Options;
using ShowVault.Agent.Recovery;

namespace ShowVault.Agent.Plugins;

public abstract class YamahaSettingsExportDiscoveryPluginBase(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : IDiscoveryPlugin
{
    internal const int MaximumConfiguredRootCount = 32;
    internal const int MaximumFileLimit = 4_096;
    internal const int MaximumDirectoryLimit = 1_024;
    internal const int MaximumRelativePathLength = 1_024;
    internal const long MaximumFileBytes = 2L * 1_024 * 1_024 * 1_024;
    internal const long MaximumTotalBytes = 16L * 1_024 * 1_024 * 1_024;
    internal static readonly TimeSpan MaximumCaptureDuration = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan MaximumPackageDuration = TimeSpan.FromMinutes(15);
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public abstract AgentPluginManifest Manifest { get; }

    protected abstract IReadOnlyList<string> ConfiguredRoots { get; }

    public async Task<DiscoveryResult> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);
        if (!Path.IsPathFullyQualified(request.RootPath))
        {
            throw new ArgumentException("Yamaha export root must be absolute.", nameof(request));
        }

        if (request.MaxFiles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "File limit must be positive.");
        }

        var rootPath = NormalizeRoot(request.RootPath);
        if (!ConfiguredRoots.Select(NormalizeRoot).Contains(rootPath, PathComparer))
        {
            throw new UnauthorizedAccessException(
                "Yamaha export root is not exactly authorized by local Agent configuration.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(MaximumCaptureDuration);
        StableSourceSnapshot snapshot;
        try
        {
            snapshot = await CaptureSnapshotAsync(
                Manifest.Id,
                rootPath,
                Math.Min(request.MaxFiles, MaximumFileLimit),
                timeout.Token);
        }
        catch (IOException exception)
        {
            throw new IOException("Yamaha settings export could not be captured safely.", exception);
        }

        await using (snapshot)
        {
            return new DiscoveryResult(
                Manifest.Id,
                Manifest.Version,
                rootPath,
                timeProvider.GetUtcNow(),
                Truncated: false,
                snapshot.Files
                    .Select(file => new DiscoveryFile(
                        file.RelativePath.Replace('/', Path.DirectorySeparatorChar),
                        file.Size,
                        file.LastModifiedAt,
                        file.Sha256))
                    .ToList());
        }
    }

    internal static async Task<StableSourceSnapshot> CaptureSnapshotAsync(
        string pluginId,
        string rootPath,
        int maximumFileCount,
        CancellationToken cancellationToken) => await CaptureSnapshotAsync(
            pluginId,
            rootPath,
            new YamahaCaptureBounds(
                maximumFileCount,
                MaximumDirectoryLimit,
                MaximumRelativePathLength,
                MaximumFileBytes,
                MaximumTotalBytes),
            cancellationToken);

    internal static async Task<StableSourceSnapshot> CaptureSnapshotAsync(
        string pluginId,
        string rootPath,
        YamahaCaptureBounds bounds,
        CancellationToken cancellationToken)
    {
        var snapshot = await StableSourceSnapshot.CaptureBoundedAsync(
            rootPath,
            bounds.MaximumFileCount,
            bounds.MaximumDirectoryCount,
            bounds.MaximumRelativePathLength,
            bounds.MaximumFileBytes,
            bounds.MaximumTotalBytes,
            cancellationToken);
        try
        {
            snapshot.RequireNoEmptyDirectories();
            if (GetRecognizedFormats(pluginId, snapshot.Files.Select(file => file.RelativePath))
                .Count == 0)
            {
                throw new InvalidOperationException(
                    "Configured directory has no recognized root-level Yamaha settings artifact.");
            }

            return snapshot;
        }
        catch
        {
            await snapshot.DisposeAsync();
            throw;
        }
    }

    internal static bool AreConfiguredRootsValid(IReadOnlyList<string> roots) =>
        roots.Count <= MaximumConfiguredRootCount &&
        roots.All(Path.IsPathFullyQualified) &&
        roots.Select(NormalizeRoot).Distinct(PathComparer).Count() == roots.Count;

    internal static bool HaveNoOverlap(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second)
    {
        if (!first.All(Path.IsPathFullyQualified) ||
            !second.All(Path.IsPathFullyQualified))
        {
            return true;
        }

        var normalizedFirst = first.Select(NormalizeRoot).ToList();
        var normalizedSecond = second.Select(NormalizeRoot).ToList();
        return !normalizedFirst.Any(firstRoot =>
            normalizedSecond.Any(secondRoot =>
                IsSameOrDescendant(firstRoot, secondRoot) ||
                IsSameOrDescendant(secondRoot, firstRoot)));
    }

    internal static string NormalizeRoot(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return relative == "." ||
            (relative != ".." &&
             !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
             !Path.IsPathFullyQualified(relative));
    }

    internal static bool IsYamahaPlugin(string pluginId) =>
        pluginId is YamahaDm7SettingsExportDiscoveryPlugin.PluginId or
            YamahaRivageSettingsExportDiscoveryPlugin.PluginId;

    internal static bool IsAuthorizedRoot(AgentOptions options, string pluginId, string rootPath)
    {
        var roots = pluginId switch
        {
            YamahaDm7SettingsExportDiscoveryPlugin.PluginId =>
                options.YamahaDm7SettingsExportRoots,
            YamahaRivageSettingsExportDiscoveryPlugin.PluginId =>
                options.YamahaRivageSettingsExportRoots,
            _ => []
        };
        return roots.Select(NormalizeRoot).Contains(NormalizeRoot(rootPath), PathComparer);
    }

    internal static IReadOnlyList<string> GetRecognizedFormats(
        string pluginId,
        IEnumerable<string> relativePaths)
    {
        var formats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in relativePaths)
        {
            if (relativePath.Contains('/') || relativePath.Contains('\\'))
            {
                continue;
            }

            var extension = Path.GetExtension(relativePath);
            if (pluginId == YamahaDm7SettingsExportDiscoveryPlugin.PluginId &&
                string.Equals(extension, ".dm7f", StringComparison.OrdinalIgnoreCase))
            {
                formats.Add(".dm7f");
            }
            else if (pluginId == YamahaRivageSettingsExportDiscoveryPlugin.PluginId &&
                     YamahaRivageSettingsExportDiscoveryPlugin.SettingsExtensions.Contains(
                         extension))
            {
                formats.Add(extension.ToUpperInvariant());
            }
        }

        return formats.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static string GetFamilyName(string pluginId) => pluginId switch
    {
        YamahaDm7SettingsExportDiscoveryPlugin.PluginId => "Yamaha DM7",
        YamahaRivageSettingsExportDiscoveryPlugin.PluginId => "Yamaha RIVAGE PM",
        _ => "Yamaha"
    };

    protected AgentOptions Options => options.Value;
}

internal sealed record YamahaCaptureBounds(
    int MaximumFileCount,
    int MaximumDirectoryCount,
    int MaximumRelativePathLength,
    long MaximumFileBytes,
    long MaximumTotalBytes);

public sealed class YamahaDm7SettingsExportDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : YamahaSettingsExportDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-dm7-settings-export";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha DM7 Settings Export Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.YamahaDm7SettingsExportRoots;
}

public sealed class YamahaRivageSettingsExportDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : YamahaSettingsExportDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-rivage-settings-export";
    internal static readonly HashSet<string> SettingsExtensions = new(
        [".RIVAGEPM", ".PM10ALL", ".PM7ALL", ".PM10PART", ".PM7PART"],
        StringComparer.OrdinalIgnoreCase);

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha RIVAGE PM Settings Export Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.YamahaRivageSettingsExportRoots;
}
