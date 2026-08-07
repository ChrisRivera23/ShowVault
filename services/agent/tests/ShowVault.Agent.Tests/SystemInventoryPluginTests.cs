using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class SystemInventoryPluginTests
{
    [Fact]
    public async Task Inventory_collects_bounded_read_only_host_metadata()
    {
        var now = DateTimeOffset.UtcNow;
        var plugin = new SystemInventoryPlugin(new FixedTimeProvider(now));

        var result = await plugin.CollectAsync(CancellationToken.None);

        Assert.Equal(SystemInventoryPlugin.PluginId, result.PluginId);
        Assert.Equal(now, result.CollectedAt);
        Assert.False(string.IsNullOrWhiteSpace(result.MachineName));
        Assert.False(string.IsNullOrWhiteSpace(result.OperatingSystem));
        Assert.True(result.LogicalProcessorCount > 0);
        Assert.True(result.Volumes.Count <= 64);
        Assert.Contains(
            AgentPluginPermission.ReadSystemInformation,
            plugin.Manifest.Permissions);
        Assert.DoesNotContain(AgentPluginPermission.ReadFiles, plugin.Manifest.Permissions);
    }

    [Fact]
    public async Task Inventory_honors_cancellation_before_reading_the_host()
    {
        var plugin = new SystemInventoryPlugin(TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            plugin.CollectAsync(cancellation.Token));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
