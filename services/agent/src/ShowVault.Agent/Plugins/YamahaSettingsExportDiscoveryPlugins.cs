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

            if (GetConflictingPrimaryFormats(
                    pluginId,
                    snapshot.Files.Select(file => file.RelativePath)).Count != 0)
            {
                throw new InvalidOperationException(
                    "Configured directory contains a settings artifact for another Yamaha family.");
            }

            return snapshot;
        }
        catch
        {
            await snapshot.DisposeAsync();
            throw;
        }
    }

    internal static bool AreConfiguredRootsValid(IReadOnlyList<string> roots)
    {
        if (roots.Count > MaximumConfiguredRootCount ||
            !roots.All(Path.IsPathFullyQualified))
        {
            return false;
        }

        var normalized = roots.Select(NormalizeRoot).ToList();
        return normalized.Distinct(PathComparer).Count() == roots.Count &&
            !normalized.Where((_, index) =>
                    normalized.Skip(index + 1).Any(other =>
                        IsSameOrDescendant(normalized[index], other) ||
                        IsSameOrDescendant(other, normalized[index])))
                .Any();
    }

    internal static bool HaveNoOverlap(params IReadOnlyList<string>[] groups)
    {
        if (groups.Any(group => !group.All(Path.IsPathFullyQualified)))
        {
            return true;
        }

        for (var firstIndex = 0; firstIndex < groups.Length; firstIndex++)
        {
            var firstRoots = groups[firstIndex].Select(NormalizeRoot).ToList();
            for (var secondIndex = firstIndex + 1; secondIndex < groups.Length; secondIndex++)
            {
                var secondRoots = groups[secondIndex].Select(NormalizeRoot).ToList();
                if (firstRoots.Any(firstRoot => secondRoots.Any(secondRoot =>
                        IsSameOrDescendant(firstRoot, secondRoot) ||
                        IsSameOrDescendant(secondRoot, firstRoot))))
                {
                    return false;
                }
            }
        }

        return true;
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
            YamahaRivageSettingsExportDiscoveryPlugin.PluginId or
            YamahaClQlSettingsExportDiscoveryPlugin.PluginId or
            YamahaTfSettingsExportDiscoveryPlugin.PluginId or
            YamahaDm3SettingsExportDiscoveryPlugin.PluginId;

    internal static bool IsAuthorizedRoot(AgentOptions options, string pluginId, string rootPath)
    {
        var roots = pluginId switch
        {
            YamahaDm7SettingsExportDiscoveryPlugin.PluginId =>
                options.YamahaDm7SettingsExportRoots,
            YamahaRivageSettingsExportDiscoveryPlugin.PluginId =>
                options.YamahaRivageSettingsExportRoots,
            YamahaClQlSettingsExportDiscoveryPlugin.PluginId =>
                options.YamahaClQlSettingsExportRoots,
            YamahaTfSettingsExportDiscoveryPlugin.PluginId =>
                options.YamahaTfSettingsExportRoots,
            YamahaDm3SettingsExportDiscoveryPlugin.PluginId =>
                options.YamahaDm3SettingsExportRoots,
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
            else if (pluginId == YamahaClQlSettingsExportDiscoveryPlugin.PluginId &&
                     string.Equals(extension, ".CLF", StringComparison.OrdinalIgnoreCase))
            {
                formats.Add(".CLF");
            }
            else if (pluginId == YamahaTfSettingsExportDiscoveryPlugin.PluginId &&
                     string.Equals(extension, ".TFF", StringComparison.OrdinalIgnoreCase))
            {
                formats.Add(".TFF");
            }
            else if (pluginId == YamahaDm3SettingsExportDiscoveryPlugin.PluginId &&
                     string.Equals(extension, ".DM3F", StringComparison.OrdinalIgnoreCase))
            {
                formats.Add(".DM3F");
            }
        }

        return formats.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static string GetFamilyName(string pluginId) => pluginId switch
    {
        YamahaDm7SettingsExportDiscoveryPlugin.PluginId => "Yamaha DM7",
        YamahaRivageSettingsExportDiscoveryPlugin.PluginId => "Yamaha RIVAGE PM",
        YamahaClQlSettingsExportDiscoveryPlugin.PluginId => "Yamaha CL/QL",
        YamahaTfSettingsExportDiscoveryPlugin.PluginId => "Yamaha TF",
        YamahaDm3SettingsExportDiscoveryPlugin.PluginId => "Yamaha DM3",
        _ => "Yamaha"
    };

    internal static IReadOnlyList<string> GetCompanionFormats(
        string pluginId,
        IEnumerable<string> relativePaths)
    {
        var companionExtensions = pluginId switch
        {
            YamahaTfSettingsExportDiscoveryPlugin.PluginId => new[] { ".TFP", ".TFS" },
            YamahaDm3SettingsExportDiscoveryPlugin.PluginId => new[] { ".DM3P", ".DM3S" },
            _ => []
        };
        if (companionExtensions.Length == 0)
        {
            return [];
        }

        return relativePaths
            .Select(Path.GetExtension)
            .Where(extension => !string.IsNullOrEmpty(extension) &&
                companionExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            .Select(extension => extension!.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetConflictingPrimaryFormats(
        string pluginId,
        IEnumerable<string> relativePaths)
    {
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in relativePaths.Select(Path.GetExtension))
        {
            if (string.IsNullOrEmpty(extension))
            {
                continue;
            }

            var owner = GetPrimaryFormatOwner(extension);
            if (owner is not null && !string.Equals(owner, pluginId, StringComparison.Ordinal))
            {
                conflicts.Add(extension.ToUpperInvariant());
            }
        }

        return conflicts.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? GetPrimaryFormatOwner(string extension)
    {
        if (string.Equals(extension, ".dm7f", StringComparison.OrdinalIgnoreCase))
        {
            return YamahaDm7SettingsExportDiscoveryPlugin.PluginId;
        }

        if (YamahaRivageSettingsExportDiscoveryPlugin.SettingsExtensions.Contains(extension))
        {
            return YamahaRivageSettingsExportDiscoveryPlugin.PluginId;
        }

        if (string.Equals(extension, ".CLF", StringComparison.OrdinalIgnoreCase))
        {
            return YamahaClQlSettingsExportDiscoveryPlugin.PluginId;
        }

        if (string.Equals(extension, ".DM3F", StringComparison.OrdinalIgnoreCase))
        {
            return YamahaDm3SettingsExportDiscoveryPlugin.PluginId;
        }

        return string.Equals(extension, ".TFF", StringComparison.OrdinalIgnoreCase)
            ? YamahaTfSettingsExportDiscoveryPlugin.PluginId
            : null;
    }

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

public sealed class YamahaClQlSettingsExportDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : YamahaSettingsExportDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-cl-ql-settings-export";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha CL/QL Settings Export Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.YamahaClQlSettingsExportRoots;
}

public sealed class YamahaTfSettingsExportDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : YamahaSettingsExportDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-tf-settings-export";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha TF Settings Export Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.YamahaTfSettingsExportRoots;
}

public sealed class YamahaDm3SettingsExportDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : YamahaSettingsExportDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.yamaha-dm3-settings-export";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Yamaha DM3 Settings Export Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots =>
        Options.YamahaDm3SettingsExportRoots;
}
