using Microsoft.Extensions.Options;
using ShowVault.Agent.Recovery;

namespace ShowVault.Agent.Plugins;

public sealed class ResolumeDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : IDiscoveryPlugin
{
    public const string PluginId = "showvault.resolume";
    internal const int MaximumFileLimit = 128;
    internal const int MaximumConfiguredRootCount = 32;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Resolume Assisted Recovery",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    public async Task<DiscoveryResult> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);
        if (!Path.IsPathFullyQualified(request.RootPath))
        {
            throw new ArgumentException("Resolume bundle root must be absolute.", nameof(request));
        }

        if (request.MaxFiles < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "File limit must be positive.");
        }

        var effectiveFileLimit = Math.Min(request.MaxFiles, MaximumFileLimit);

        var rootPath = NormalizeRoot(request.RootPath);
        var allowedRoots = options.Value.ResolumeDiscoveryRoots
            .Select(NormalizeRoot)
            .ToHashSet(PathComparer);
        if (!allowedRoots.Contains(rootPath))
        {
            throw new UnauthorizedAccessException(
                "Resolume bundle root is not exactly authorized by local Agent configuration.");
        }

        StableSourceSnapshot snapshot;
        try
        {
            snapshot = await StableSourceSnapshot.CaptureAsync(
                rootPath,
                effectiveFileLimit,
                cancellationToken);
        }
        catch (IOException exception)
        {
            throw new IOException("Resolume bundle could not be captured safely.", exception);
        }

        await using (snapshot)
        {
            if (!snapshot.Files.Any(file =>
                    !file.RelativePath.Contains('/', StringComparison.Ordinal) &&
                    string.Equals(
                        Path.GetExtension(file.RelativePath),
                        ".avc",
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "Resolume bundle root does not contain a regular root-level composition.");
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

    internal static string NormalizeRoot(string rootPath) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
}
