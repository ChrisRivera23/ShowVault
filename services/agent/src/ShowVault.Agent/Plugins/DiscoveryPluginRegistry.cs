namespace ShowVault.Agent.Plugins;

public sealed class DiscoveryPluginRegistry(IEnumerable<IDiscoveryPlugin> plugins)
{
    private readonly IReadOnlyDictionary<string, IDiscoveryPlugin> _plugins = plugins
        .ToDictionary(plugin => plugin.Manifest.Id, StringComparer.Ordinal);

    public IDiscoveryPlugin GetRequired(string pluginId)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
        {
            throw new InvalidOperationException($"Discovery plugin is not installed: {pluginId}");
        }

        return plugin;
    }
}
