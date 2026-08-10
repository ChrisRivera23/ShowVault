using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShowVault.Api.HostedSync;

public static partial class HostedManifestValidator
{
    private const int MaxManifestBytes = 2 * 1024 * 1024;
    private const int MaxFiles = 10_000;
    private const long MaxFileBytes = 512L * 1024 * 1024;
    private const long MaxTotalBytes = 5L * 1024 * 1024 * 1024;

    private sealed record CatalogEntry(string PluginId, string ProductName);

    private static readonly IReadOnlyDictionary<string, CatalogEntry> Catalog =
        new Dictionary<string, CatalogEntry>(StringComparer.Ordinal)
        {
            ["macos.resolume-arena.user-data"] = new(
                "showvault.resolume", "Resolume Arena"),
            ["macos.serato-dj-pro.user-data"] = new(
                "showvault.serato-dj-pro", "Serato DJ Pro"),
            ["windows.resolume-arena.user-data"] = new(
                "showvault.resolume", "Resolume Arena"),
            ["windows.serato-dj-pro.user-data"] = new(
                "showvault.serato-dj-pro", "Serato DJ Pro")
        };

    public static ValidatedHostedManifest Validate(string packageId, byte[] bytes)
    {
        if (!Sha256Regex().IsMatch(packageId) || bytes.Length is 0 or > MaxManifestBytes)
        {
            throw new HostedSyncValidationException("The hosted manifest identity is invalid.");
        }

        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                MaxDepth = 16,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            var root = document.RootElement;
            RequireObjectProperties(root,
                "formatVersion", "packageId", "createdAt", "source", "files",
                "localManifestSha256");
            if (root.GetProperty("formatVersion").GetString() != "showvault.remote-package.v1" ||
                root.GetProperty("packageId").GetString() != packageId ||
                root.GetProperty("localManifestSha256").GetString() != packageId ||
                !DateTimeOffset.TryParse(root.GetProperty("createdAt").GetString(), out _))
            {
                throw new HostedSyncValidationException("The hosted manifest identity does not match.");
            }

            var source = root.GetProperty("source");
            RequireObjectProperties(source, "candidateKey", "pluginId", "productName");
            var candidateKey = BoundedString(source, "candidateKey", 120);
            var pluginId = BoundedString(source, "pluginId", 200);
            var productName = BoundedString(source, "productName", 200);
            if (!Catalog.TryGetValue(candidateKey, out var catalog) ||
                catalog.PluginId != pluginId || catalog.ProductName != productName)
            {
                throw new HostedSyncValidationException(
                    "The hosted manifest source is not an approved catalog recovery source.");
            }

            var filesElement = root.GetProperty("files");
            if (filesElement.ValueKind != JsonValueKind.Array ||
                filesElement.GetArrayLength() is 0 or > MaxFiles)
            {
                throw new HostedSyncValidationException("The hosted manifest file count is invalid.");
            }

            var files = new Dictionary<string, HostedSyncDescriptor>(StringComparer.Ordinal);
            long totalBytes = 0;
            foreach (var file in filesElement.EnumerateArray())
            {
                RequireObjectProperties(file, "relativePath", "size", "sha256");
                var relativePath = BoundedString(file, "relativePath", 4096);
                ValidateLogicalPath(relativePath);
                if (!file.GetProperty("size").TryGetInt64(out var size) ||
                    size is < 0 or > MaxFileBytes)
                {
                    throw new HostedSyncValidationException("A hosted file size is invalid.");
                }
                var digest = BoundedString(file, "sha256", 64);
                if (!Sha256Regex().IsMatch(digest) ||
                    !files.TryAdd(relativePath, new HostedSyncDescriptor(relativePath, size, digest)))
                {
                    throw new HostedSyncValidationException(
                        "A hosted file identity is invalid or duplicated.");
                }
                totalBytes = checked(totalBytes + size);
                if (totalBytes > MaxTotalBytes)
                {
                    throw new HostedSyncValidationException("The hosted package is oversized.");
                }
            }

            return new ValidatedHostedManifest(
                packageId,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                bytes,
                files);
        }
        catch (HostedSyncValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or FormatException or OverflowException)
        {
            throw new HostedSyncValidationException("The hosted manifest is malformed.");
        }
    }

    public static string[] ValidateLogicalPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 4096 ||
            path.StartsWith('/') || path.Contains('\\'))
        {
            throw new HostedSyncValidationException("A hosted logical path is unsafe.");
        }
        var segments = path.Split('/');
        if (segments.Any(segment => segment.Length is 0 or > 255 || segment is "." or ".."))
        {
            throw new HostedSyncValidationException("A hosted logical path is unsafe.");
        }
        return segments;
    }

    private static string BoundedString(JsonElement parent, string property, int max)
    {
        var element = parent.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String ||
            element.GetString() is not { Length: > 0 } value || value.Length > max)
        {
            throw new HostedSyncValidationException("Hosted manifest metadata is invalid.");
        }
        return value;
    }

    private static void RequireObjectProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new HostedSyncValidationException("The hosted manifest shape is invalid.");
        }
        var actual = element.EnumerateObject().Select(property => property.Name).ToHashSet(
            StringComparer.Ordinal);
        if (actual.Count != expected.Length || expected.Any(name => !actual.Contains(name)))
        {
            throw new HostedSyncValidationException(
                "The hosted manifest contains unsupported metadata.");
        }
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();
}
