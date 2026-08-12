using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class SystemInventoryPluginTests
{
    [Fact]
    public async Task Inventory_collects_at_most_64_synthetic_volume_records()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new TestSystemInventorySource(
            new SystemInventoryHostFacts(
                "synthetic-machine",
                "Synthetic OS",
                "X64",
                "Arm64",
                8),
            Enumerable.Range(0, 70)
                .Select(index => new SystemVolume(
                    $"synthetic-volume-{index}",
                    "Fixed",
                    1_000 + index,
                    500 + index)));
        var plugin = new SystemInventoryPlugin(new FixedTimeProvider(now), source);

        var result = await plugin.CollectAsync(CancellationToken.None);

        Assert.Equal(SystemInventoryPlugin.PluginId, result.PluginId);
        Assert.Equal(now, result.CollectedAt);
        Assert.Equal("synthetic-machine", result.MachineName);
        Assert.Equal("Synthetic OS", result.OperatingSystem);
        Assert.Equal("X64", result.OsArchitecture);
        Assert.Equal("Arm64", result.ProcessArchitecture);
        Assert.Equal(8, result.LogicalProcessorCount);
        Assert.Equal(64, result.Volumes.Count);
        Assert.Equal("synthetic-volume-0", result.Volumes[0].Name);
        Assert.Equal("synthetic-volume-63", result.Volumes[^1].Name);
        Assert.Contains(
            AgentPluginPermission.ReadSystemInformation,
            plugin.Manifest.Permissions);
        Assert.DoesNotContain(AgentPluginPermission.ReadFiles, plugin.Manifest.Permissions);
    }

    [Fact]
    public async Task Inventory_honors_cancellation_before_reading_the_host()
    {
        var source = new TestSystemInventorySource(
            new SystemInventoryHostFacts("unused", "unused", "unused", "unused", 1),
            []);
        var plugin = new SystemInventoryPlugin(TimeProvider.System, source);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            plugin.CollectAsync(cancellation.Token));
        Assert.Equal(0, source.HostReadCount);
        Assert.Equal(0, source.VolumeEnumerationCount);
    }

    [Fact]
    public async Task Inventory_honors_cancellation_during_volume_enumeration()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new TestSystemInventorySource(
            new SystemInventoryHostFacts("synthetic", "Synthetic OS", "X64", "X64", 4),
            CancelDuringEnumeration(cancellation));
        var plugin = new SystemInventoryPlugin(TimeProvider.System, source);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            plugin.CollectAsync(cancellation.Token));
        Assert.Equal(1, source.HostReadCount);
    }

    [Fact]
    public async Task Inventory_rejects_unbounded_synthetic_host_metadata()
    {
        var source = new TestSystemInventorySource(
            new SystemInventoryHostFacts(
                new string('m', 256),
                "Synthetic OS",
                "X64",
                "X64",
                4),
            []);
        var plugin = new SystemInventoryPlugin(TimeProvider.System, source);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            plugin.CollectAsync(CancellationToken.None));

        Assert.DoesNotContain(new string('m', 256), error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1L, null)]
    [InlineData(null, 1L)]
    [InlineData(100L, -1L)]
    [InlineData(100L, 101L)]
    public async Task Inventory_rejects_invalid_synthetic_volume_capacity(
        long? totalBytes,
        long? availableBytes)
    {
        var source = new TestSystemInventorySource(
            new SystemInventoryHostFacts("synthetic", "Synthetic OS", "X64", "X64", 4),
            [new SystemVolume("synthetic-volume", "Fixed", totalBytes, availableBytes)]);
        var plugin = new SystemInventoryPlugin(TimeProvider.System, source);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            plugin.CollectAsync(CancellationToken.None));
    }

    private static IEnumerable<SystemVolume> CancelDuringEnumeration(
        CancellationTokenSource cancellation)
    {
        yield return new SystemVolume("first", "Fixed", 100, 50);
        cancellation.Cancel();
        yield return new SystemVolume("must-not-be-added", "Fixed", 100, 50);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestSystemInventorySource(
        SystemInventoryHostFacts hostFacts,
        IEnumerable<SystemVolume> volumes) : ISystemInventorySource
    {
        public int HostReadCount { get; private set; }

        public int VolumeEnumerationCount { get; private set; }

        public SystemInventoryHostFacts ReadHostFacts()
        {
            HostReadCount++;
            return hostFacts;
        }

        public IEnumerable<SystemVolume> EnumerateVolumes()
        {
            VolumeEnumerationCount++;
            return volumes;
        }
    }
}
