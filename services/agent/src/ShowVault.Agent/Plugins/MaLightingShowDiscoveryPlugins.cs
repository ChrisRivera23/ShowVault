using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public abstract class MaLightingShowDiscoveryPluginBase(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : IDiscoveryPlugin
{
    private const int MaximumFileLimit = 100_000;

    public abstract AgentPluginManifest Manifest { get; }

    protected abstract IReadOnlyList<string> ConfiguredRoots { get; }

    protected abstract bool HasExpectedStructure(string rootPath);

    protected abstract string ProductName { get; }

    public async Task<DiscoveryResult> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);
        if (!Path.IsPathFullyQualified(request.RootPath))
        {
            throw new ArgumentException(
                $"{ProductName} export root must be an absolute path.",
                nameof(request));
        }

        if (request.MaxFiles is < 1 or > MaximumFileLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"File limit must be between 1 and {MaximumFileLimit}.");
        }

        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.RootPath));
        if (!ConfiguredRoots.Any(configuredRoot => IsSamePath(rootPath, configuredRoot)))
        {
            throw new UnauthorizedAccessException(
                $"{ProductName} export root is not allowed by local Agent configuration: {rootPath}");
        }

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException(
                $"{ProductName} export root does not exist: {rootPath}");
        }

        if (!HasExpectedStructure(rootPath))
        {
            throw new InvalidOperationException(
                $"Configured directory is not a recognized {ProductName} export root.");
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

    protected static bool HasDirectory(string rootPath, params string[] segments) =>
        Directory.Exists(Path.Combine([rootPath, .. segments]));

    private static bool IsSamePath(string requestedPath, string configuredRoot) =>
        Path.GetRelativePath(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot)),
            requestedPath) == ".";

    protected AgentOptions Options => options.Value;
}

public sealed class GrandMa2ShowDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : MaLightingShowDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.malighting-grandma2";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault MA Lighting grandMA2 Export",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.GrandMa2ExportRoots;

    protected override string ProductName => "grandMA2";

    protected override bool HasExpectedStructure(string rootPath)
    {
        if (!string.Equals(Path.GetFileName(rootPath), "gma2", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HasDirectory(rootPath, "shows") ||
            Directory.EnumerateDirectories(rootPath)
                .Any(versionRoot => HasDirectory(versionRoot, "shows"));
    }
}

public sealed class GrandMa3ShowDiscoveryPlugin(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider) : MaLightingShowDiscoveryPluginBase(options, timeProvider)
{
    public const string PluginId = "showvault.malighting-grandma3";

    public override AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault MA Lighting grandMA3 Export",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.Discovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ReadFiles });

    protected override IReadOnlyList<string> ConfiguredRoots => Options.GrandMa3ExportRoots;

    protected override string ProductName => "grandMA3";

    protected override bool HasExpectedStructure(string rootPath) =>
        string.Equals(Path.GetFileName(rootPath), "grandMA3", StringComparison.OrdinalIgnoreCase) &&
        (HasDirectory(rootPath, "shared", "shows") ||
         HasDirectory(rootPath, "shared", "backups") ||
         HasDirectory(rootPath, "gma3_library"));
}
