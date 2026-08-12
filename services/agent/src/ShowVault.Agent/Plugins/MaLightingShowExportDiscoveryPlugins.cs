using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Recovery;

namespace ShowVault.Agent.Plugins;

public abstract partial class MaLightingShowExportDiscoveryPluginBase(
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

    protected abstract bool IsRecognizedExportRoot(string rootPath);

    protected abstract string ProductName { get; }

    public async Task<DiscoveryResult> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);
        if (!Path.IsPathFullyQualified(request.RootPath))
        {
            throw new ArgumentException("grandMA export root must be absolute.", nameof(request));
        }

        if (request.MaxFiles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "File limit must be positive.");
        }

        var rootPath = NormalizeRoot(request.RootPath);
        if (!ConfiguredRoots.Select(NormalizeRoot).Contains(rootPath, PathComparer))
        {
            throw new UnauthorizedAccessException(
                "grandMA export root is not exactly authorized by local Agent configuration.");
        }

        if (!IsRecognizedExportRoot(rootPath))
        {
            throw new InvalidOperationException(
                $"Configured root is not a recognized {ProductName} show export directory.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(MaximumCaptureDuration);
        StableSourceSnapshot snapshot;
        try
        {
            snapshot = await CaptureSnapshotAsync(
                rootPath,
                Math.Min(request.MaxFiles, MaximumFileLimit),
                timeout.Token);
        }
        catch (IOException exception)
        {
            throw new IOException("grandMA show export could not be captured safely.", exception);
        }

        await using (snapshot)
        {
            if (snapshot.Files.Count == 0)
            {
                throw new InvalidOperationException("grandMA show export contains no files.");
            }

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
        string rootPath,
        int maximumFileCount,
        CancellationToken cancellationToken)
    {
        var snapshot = await StableSourceSnapshot.CaptureBoundedAsync(
            rootPath,
            maximumFileCount,
            MaximumDirectoryLimit,
            MaximumRelativePathLength,
            MaximumFileBytes,
            MaximumTotalBytes,
            cancellationToken);
        try
        {
            snapshot.RequireNoEmptyDirectories();
            return snapshot;
        }
        catch
        {
            await snapshot.DisposeAsync();
            throw;
        }
    }

    internal static string NormalizeRoot(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    internal static bool IsMaLightingPlugin(string pluginId) =>
        pluginId is GrandMa2ShowExportDiscoveryPlugin.PluginId or
            GrandMa3ShowExportDiscoveryPlugin.PluginId;

    internal static bool IsAuthorizedRoot(AgentOptions options, string pluginId, string rootPath)
    {
        var roots = pluginId switch
        {
            GrandMa2ShowExportDiscoveryPlugin.PluginId => options.GrandMa2ShowExportRoots,
            GrandMa3ShowExportDiscoveryPlugin.PluginId => options.GrandMa3ShowExportRoots,
            _ => []
        };
        return roots.Select(NormalizeRoot).Contains(NormalizeRoot(rootPath), PathComparer);
    }

    internal static bool IsRecognizedRoot(string pluginId, string rootPath) => pluginId switch
    {
        GrandMa2ShowExportDiscoveryPlugin.PluginId =>
            GrandMa2ShowExportDiscoveryPlugin.IsRecognizedRoot(rootPath),
        GrandMa3ShowExportDiscoveryPlugin.PluginId =>
            GrandMa3ShowExportDiscoveryPlugin.IsRecognizedRoot(rootPath),
        _ => false
    };

    internal static string? GetProductVersion(string pluginId, string rootPath) =>
        pluginId == GrandMa2ShowExportDiscoveryPlugin.PluginId
            ? GrandMa2ShowExportDiscoveryPlugin.GetVersion(rootPath)
            : null;

    [GeneratedRegex("^[0-9]+(?:\\.[0-9]+){1,2}$", RegexOptions.CultureInvariant)]
    protected static partial Regex GrandMa2VersionPattern();

    protected AgentOptions Options => options.Value;
}

public sealed class GrandMa2ShowExportDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : MaLightingShowExportDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.malighting-grandma2-show-export";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault grandMA2 Show Export Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.GrandMa2ShowExportRoots;

    protected override string ProductName => "grandMA2";

    protected override bool IsRecognizedExportRoot(string rootPath) =>
        IsRecognizedRoot(rootPath);

    internal static bool IsRecognizedRoot(string rootPath) =>
        GetVersion(rootPath) is not null || IsUnversionedRoot(rootPath);

    internal static string? GetVersion(string rootPath)
    {
        if (!string.Equals(Path.GetFileName(rootPath), "shows", StringComparison.Ordinal))
        {
            return null;
        }

        var versionDirectory = Directory.GetParent(rootPath);
        var productDirectory = versionDirectory?.Parent;
        return versionDirectory is not null && productDirectory is not null &&
            string.Equals(productDirectory.Name, "gma2", StringComparison.Ordinal) &&
            GrandMa2VersionPattern().IsMatch(versionDirectory.Name)
                ? versionDirectory.Name
                : null;
    }

    private static bool IsUnversionedRoot(string rootPath)
    {
        var parent = Directory.GetParent(rootPath);
        return string.Equals(Path.GetFileName(rootPath), "shows", StringComparison.Ordinal) &&
            parent is not null &&
            string.Equals(parent.Name, "gma2", StringComparison.Ordinal);
    }
}

public sealed class GrandMa3ShowExportDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : MaLightingShowExportDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.malighting-grandma3-show-export";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault grandMA3 Show Export Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.GrandMa3ShowExportRoots;

    protected override string ProductName => "grandMA3";

    protected override bool IsRecognizedExportRoot(string rootPath)
        => IsRecognizedRoot(rootPath);

    internal static bool IsRecognizedRoot(string rootPath)
    {
        var exportDirectory = new DirectoryInfo(rootPath);
        var sharedDirectory = exportDirectory.Parent;
        var productDirectory = sharedDirectory?.Parent;
        return exportDirectory.Name is "shows" or "backups" &&
            sharedDirectory is not null &&
            string.Equals(sharedDirectory.Name, "shared", StringComparison.Ordinal) &&
            productDirectory is not null &&
            string.Equals(productDirectory.Name, "grandMA3", StringComparison.Ordinal);
    }
}
