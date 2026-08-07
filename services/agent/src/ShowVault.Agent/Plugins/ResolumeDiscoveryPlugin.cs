using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed class ResolumeDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : IDiscoveryPlugin
{
    public const string PluginId = "showvault.resolume";
    private const int MaximumFileLimit = 100_000;
    private static readonly HashSet<string> UserDataDirectories = new(
        [
            "Compositions",
            "Extra Effects",
            "Fixture Library",
            "Preferences",
            "Presets",
            "Recorded",
            "Shortcuts"
        ],
        StringComparer.OrdinalIgnoreCase);

    public AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Resolume Recovery",
        "0.2.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    public async Task<DiscoveryResult> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);
        if (!Path.IsPathFullyQualified(request.RootPath))
        {
            throw new ArgumentException("Resolume bundle root must be an absolute path.", nameof(request));
        }

        if (request.MaxFiles is < 1 or > MaximumFileLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"File limit must be between 1 and {MaximumFileLimit}.");
        }

        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.RootPath));
        var isPortableBundle = options.Value.ResolumeDiscoveryRoots.Any(
            allowedRoot => IsWithinRoot(rootPath, allowedRoot));
        var isUserDataRoot = options.Value.ResolumeUserDataRoots.Any(
            allowedRoot => IsSamePath(rootPath, allowedRoot));
        if (!isPortableBundle && !isUserDataRoot)
        {
            throw new UnauthorizedAccessException(
                $"Resolume recovery root is not allowed by the local Agent configuration: {rootPath}");
        }

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Resolume recovery root does not exist: {rootPath}");
        }

        if (isUserDataRoot && !HasResolumeUserDataDirectory(rootPath))
        {
            throw new InvalidOperationException(
                "Resolume user-data root does not contain a recognized product directory.");
        }

        var files = new List<DiscoveryFile>();
        var truncated = false;
        foreach (var path in Directory.EnumerateFiles(
            rootPath,
            "*",
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (files.Count == request.MaxFiles)
            {
                truncated = true;
                break;
            }

            var info = new FileInfo(path);
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65_536,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            files.Add(new DiscoveryFile(
                Path.GetRelativePath(rootPath, path),
                info.Length,
                info.LastWriteTimeUtc,
                Convert.ToHexStringLower(hash)));
        }

        return new DiscoveryResult(
            Manifest.Id,
            Manifest.Version,
            rootPath,
            timeProvider.GetUtcNow(),
            truncated,
            files);
    }

    private static bool IsWithinRoot(string requestedPath, string configuredRoot)
    {
        var allowedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
        var relativePath = Path.GetRelativePath(allowedPath, requestedPath);
        return relativePath == "." ||
            (!Path.IsPathFullyQualified(relativePath) &&
             relativePath != ".." &&
             !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
             !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static bool IsSamePath(string requestedPath, string configuredRoot) =>
        Path.GetRelativePath(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot)),
            requestedPath) == ".";

    private static bool HasResolumeUserDataDirectory(string rootPath) =>
        Directory.EnumerateDirectories(rootPath)
            .Select(Path.GetFileName)
            .Any(directory => directory is not null && UserDataDirectories.Contains(directory));
}
