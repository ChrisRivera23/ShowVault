using ShowVault.Agent.Recovery;

namespace ShowVault.LocalEngine;

internal sealed class LocalVaultLayout : IDisposable
{
    public const string QueueDatabaseName = "local-engine.db";
    public static readonly string[] RequiredDirectories =
    [
        "Backups", "Manifests", "Device Exports", "Upload Queue",
        "Reports", "Logs", "Quarantine"
    ];

    private LocalVaultLayout(string rootPath, StableDirectoryTree root)
    {
        RootPath = rootPath;
        Root = root;
    }

    public string RootPath { get; }
    public StableDirectoryTree Root { get; }

    public static LocalVaultLayout OpenOrCreate(string selectedVault)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedVault));
        StableDirectoryTree root;
        try
        {
            root = StableDirectoryTree.OpenReadOnlyNoFollowPath(fullPath);
        }
        catch (IOException)
        {
            var parentPath = Path.GetDirectoryName(fullPath);
            var leafName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(leafName))
            {
                throw new LocalEngineException("The selected local vault is invalid.");
            }
            using var parent = StableDirectoryTree.OpenReadOnlyNoFollowPath(parentPath);
            var comparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            if (parent.EnumerateNames().Contains(leafName, comparer))
            {
                throw new LocalEngineException("The selected local vault is linked or invalid.");
            }
            root = parent.CreateDirectory(leafName);
        }
        try
        {
            foreach (var name in RequiredDirectories)
            {
                using var directory = root.GetOrCreateDirectory(name);
                if (!root.HasSameVolume(directory))
                {
                    throw new LocalEngineException(
                        "The local vault contains a mounted filesystem substitution.");
                }
            }
            return new(fullPath, root);
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    public string QueueDatabasePath => Path.Combine(
        RootPath, "Upload Queue", QueueDatabaseName);

    public void Dispose() => Root.Dispose();
}
