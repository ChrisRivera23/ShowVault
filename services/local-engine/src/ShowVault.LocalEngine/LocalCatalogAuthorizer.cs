using ShowVault.Agent.Recovery;

namespace ShowVault.LocalEngine;

internal sealed record AuthorizedCatalogSource(
    string CandidateKey,
    string PluginId,
    string ProductName,
    string ExpectedPath);

public sealed class LocalCatalogAuthorizer
{
    private readonly IReadOnlyDictionary<string, AuthorizedCatalogSource> _sources;

    public LocalCatalogAuthorizer(
        IReadOnlyDictionary<string, string>? environment = null,
        string? syntheticHome = null)
    {
        environment ??= Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key is string && entry.Value is string)
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
        var home = syntheticHome ?? Get(environment, "HOME");
        var profile = syntheticHome ?? Get(environment, "USERPROFILE");
        var definitions = new List<AuthorizedCatalogSource>();
        if (!string.IsNullOrWhiteSpace(home))
        {
            definitions.Add(new(
                "macos.resolume-arena.user-data", "showvault.resolume",
                "Resolume Arena", Path.Combine(home, "Documents", "Resolume Arena")));
            definitions.Add(new(
                "macos.serato-dj-pro.user-data", "showvault.serato-dj-pro",
                "Serato DJ Pro", Path.Combine(home, "Music", "_Serato_")));
        }
        if (!string.IsNullOrWhiteSpace(profile))
        {
            definitions.Add(new(
                "windows.resolume-arena.user-data", "showvault.resolume",
                "Resolume Arena", Path.Combine(profile, "Documents", "Resolume Arena")));
            definitions.Add(new(
                "windows.serato-dj-pro.user-data", "showvault.serato-dj-pro",
                "Serato DJ Pro", Path.Combine(profile, "Music", "_Serato_")));
        }

        _sources = definitions.ToDictionary(source => source.CandidateKey, StringComparer.Ordinal);
    }

    internal AuthorizedCatalogSource Authorize(string candidateKey, string selectedSource)
    {
        if (!_sources.TryGetValue(candidateKey, out var source))
        {
            throw new LocalEngineException("The selected detection cannot be saved.");
        }

        using var expected = StableDirectoryTree.OpenReadOnlyNoFollowPath(source.ExpectedPath);
        using var selected = StableDirectoryTree.OpenReadOnlyNoFollowPath(selectedSource);
        if (!expected.HasSameIdentity(selected))
        {
            throw new LocalEngineException(
                "The selected folder does not match the exact detected user-data source.");
        }

        return source;
    }

    private static string? Get(IReadOnlyDictionary<string, string> environment, string key) =>
        environment.TryGetValue(key, out var value) ? value : null;
}
