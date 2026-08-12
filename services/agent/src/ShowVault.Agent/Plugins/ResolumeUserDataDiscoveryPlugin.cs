using Microsoft.Extensions.Options;
using ShowVault.Agent.Recovery;

namespace ShowVault.Agent.Plugins;

public sealed class ResolumeUserDataDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : IDiscoveryPlugin
{
    public const string PluginId = "showvault.resolume-user-data";
    internal const int MaximumConfiguredRootCount = 32;
    internal const int MaximumFileLimit = 2_048;
    internal const int MaximumDirectoryLimit = 256;
    internal const int MaximumRelativePathLength = 1_024;
    internal const long MaximumFileBytes = 16L * 1_024 * 1_024;
    internal const long MaximumTotalBytes = 128L * 1_024 * 1_024;
    internal static readonly TimeSpan MaximumCaptureDuration = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan MaximumPackageDuration = TimeSpan.FromMinutes(2);
    internal static readonly IReadOnlySet<string> SupportedCategories = new HashSet<string>(
        [
            "Compositions",
            "Fixture Library",
            "Preferences",
            "Presets",
            "Shortcuts"
        ],
        StringComparer.Ordinal);
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Resolume User Data Assisted Recovery",
        "1.0.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    public async Task<DiscoveryResult> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);
        if (!Path.IsPathFullyQualified(request.RootPath))
        {
            throw new ArgumentException("Resolume user-data root must be absolute.", nameof(request));
        }

        if (request.MaxFiles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "File limit must be positive.");
        }

        var rootPath = ResolumeDiscoveryPlugin.NormalizeRoot(request.RootPath);
        var allowedRoots = options.Value.ResolumeUserDataRoots
            .Select(ResolumeDiscoveryPlugin.NormalizeRoot)
            .ToHashSet(PathComparer);
        if (!allowedRoots.Contains(rootPath))
        {
            throw new UnauthorizedAccessException(
                "Resolume user-data root is not exactly authorized by local Agent configuration.");
        }

        if (options.Value.ResolumeDiscoveryRoots
            .Select(ResolumeDiscoveryPlugin.NormalizeRoot)
            .Contains(rootPath, PathComparer))
        {
            throw new InvalidOperationException(
                "Resolume root has ambiguous portable-bundle and user-data profiles.");
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
            throw new IOException("Resolume user data could not be captured safely.", exception);
        }

        await using (snapshot)
        {
            if (snapshot.Files.Count == 0)
            {
                throw new InvalidOperationException(
                    "Resolume user-data root has no files in supported recovery categories.");
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
        var snapshot = await StableSourceSnapshot.CaptureSelectedRootDirectoriesAsync(
            rootPath,
            SupportedCategories,
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
}
