using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShowVault.Api.HostedSync;

internal static partial class HostedSyncValidator
{
    public const int MaximumChunkBytes = 256 * 1024;
    public const int MaximumFiles = 10_000;
    public const long MaximumTotalBytes = 5L * 1024 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<string, string> Catalog =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["macos.resolume-arena.user-data"] = "showvault.resolume",
            ["macos.serato-dj-pro.user-data"] = "showvault.serato-dj-pro",
            ["windows.resolume-arena.user-data"] = "showvault.resolume",
            ["windows.serato-dj-pro.user-data"] = "showvault.serato-dj-pro"
        };

    public static bool TryValidate(
        string recoveryPointId,
        HostedSyncBeginRequest request,
        out string canonicalJson)
    {
        canonicalJson = "";
        var manifest = request.Manifest;
        if (manifest is null || manifest.Files is null || manifest.FormatVersion != "1.0" ||
            !Digest().IsMatch(recoveryPointId) || manifest.RecoveryPointId != recoveryPointId ||
            manifest.ManifestSha256 != recoveryPointId || !Digest().IsMatch(request.ManifestDigest) ||
            !Catalog.TryGetValue(manifest.CandidateKey, out var plugin) ||
            plugin != manifest.PluginId || manifest.FileCount != manifest.Files.Count ||
            manifest.FileCount is < 1 or > MaximumFiles || manifest.TotalBytes is < 0 or > MaximumTotalBytes ||
            manifest.Files.Sum(file => file.Size) != manifest.TotalBytes)
            return false;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            if (!ValidRelativePath(file.RelativePath) || file.Size is < 0 or > MaximumTotalBytes ||
                !Digest().IsMatch(file.Sha256) || !paths.Add(file.RelativePath)) return false;
        }
        canonicalJson = JsonSerializer.Serialize(manifest, JsonOptions);
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
        return digest == request.ManifestDigest;
    }

    public static bool ValidRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 ||
            value.StartsWith('/') || value.EndsWith('/') || value.Contains('\\') ||
            value != value.Normalize(NormalizationForm.FormC) ||
            value.Any(char.IsControl)) return false;
        var segments = value.Split('/');
        return segments.All(segment => segment.Length is > 0 and <= 255 &&
            segment is not ("." or "..") && segment.IndexOf('\0') < 0);
    }

    public static string ObjectKey(Guid sessionId, string relativePath)
    {
        var pathHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(relativePath)));
        return $"sessions/{sessionId:N}/files/{pathHash}";
    }

    public static string ObjectPrefix(Guid sessionId) => $"sessions/{sessionId:N}/files/";

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Digest();
}
