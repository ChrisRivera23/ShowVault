using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Recovery;

public sealed class LocalVaultLayout
{
    public const string DefaultDirectoryName = "ShowVault Pro";
    public const string BackupsDirectoryName = "Backups";
    public const string ManifestsDirectoryName = "Manifests";
    public const string DeviceExportsDirectoryName = "Device Exports";
    public const string UploadQueueDirectoryName = "Upload Queue";
    public const string ReportsDirectoryName = "Reports";
    public const string LogsDirectoryName = "Logs";
    public const string QuarantineDirectoryName = "Quarantine";

    private static readonly string[] RequiredDirectoryNames =
    [
        BackupsDirectoryName,
        ManifestsDirectoryName,
        DeviceExportsDirectoryName,
        UploadQueueDirectoryName,
        ReportsDirectoryName,
        LogsDirectoryName,
        QuarantineDirectoryName
    ];

    public LocalVaultLayout(IOptions<AgentOptions> options)
    {
        RootPath = ResolveRootPath(options.Value);
    }

    public string RootPath { get; }

    public string BackupsPath => Path.Combine(RootPath, BackupsDirectoryName);

    public string UploadQueuePath => Path.Combine(RootPath, UploadQueueDirectoryName);

    public void EnsureInitialized()
    {
        Directory.CreateDirectory(RootPath);
        foreach (var directoryName in RequiredDirectoryNames)
        {
            Directory.CreateDirectory(Path.Combine(RootPath, directoryName));
        }
    }

    public string GetRecoveryPointPath(
        string parentIdentity,
        DateTimeOffset createdAt,
        string recoveryPointId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryPointId);
        if (recoveryPointId.Length != 64 || !recoveryPointId.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "A recovery-point ID must be a SHA-256 hexadecimal value.",
                nameof(recoveryPointId));
        }

        var parentDirectory = SanitizeDirectoryName(parentIdentity);
        var recoveryPointDirectory =
            $"{createdAt.ToUniversalTime():yyyy-MM-dd'T'HH-mm-ss'Z'}__{recoveryPointId.ToLowerInvariant()}";
        return Path.Combine(BackupsPath, parentDirectory, recoveryPointDirectory);
    }

    private static string ResolveRootPath(AgentOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.VaultDirectory))
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.VaultDirectory));
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            throw new InvalidOperationException(
                "The Documents directory is unavailable. Configure Agent:VaultDirectory explicitly.");
        }

        return Path.Combine(documents, DefaultDirectoryName);
    }

    private static string SanitizeDirectoryName(string value)
    {
        var name = value.StartsWith("showvault.", StringComparison.OrdinalIgnoreCase)
            ? value["showvault.".Length..]
            : value;
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var characters = name
            .Select(character => invalid.Contains(character) || char.IsControl(character) ? '-' : character)
            .ToArray();
        var sanitized = new string(characters).Trim().Trim('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown System" : sanitized;
    }
}
