using CrestApps.OrchardCore.ContactCenter.Core.Services;
using OrchardCore.Environment.Cache;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterConfigurationCacheTests
{
    private sealed class SampleEntry
    {
        public string Name { get; set; }
    }

    private sealed class OtherEntry
    {
        public string Name { get; set; }
    }

    private static ContactCenterConfigurationCache CreateCache(out ISignal signal)
    {
        signal = new Signal();

        return new ContactCenterConfigurationCache(signal);
    }

    [Fact]
    public async Task GetEnabledAsync_CachesTheSnapshot_AndDoesNotReloadUntilInvalidated()
    {
        // Arrange
        var cache = CreateCache(out _);
        var loadCount = 0;

        Func<CancellationToken, Task<IReadOnlyCollection<SampleEntry>>> factory = _ =>
        {
            loadCount++;

            return Task.FromResult<IReadOnlyCollection<SampleEntry>>([new SampleEntry { Name = "a" }]);
        };

        // Act
        var first = await cache.GetEnabledAsync(factory, TestContext.Current.CancellationToken);
        var second = await cache.GetEnabledAsync(factory, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, loadCount);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetEnabledAsync_ReloadsAfterInvalidation()
    {
        // Arrange
        var cache = CreateCache(out _);
        var loadCount = 0;

        Func<CancellationToken, Task<IReadOnlyCollection<SampleEntry>>> factory = _ =>
        {
            loadCount++;

            return Task.FromResult<IReadOnlyCollection<SampleEntry>>([new SampleEntry { Name = "a" + loadCount }]);
        };

        // Act
        await cache.GetEnabledAsync(factory, TestContext.Current.CancellationToken);
        await cache.InvalidateEnabledAsync<SampleEntry>();
        var refreshed = await cache.GetEnabledAsync(factory, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, loadCount);
        Assert.Equal("a2", Assert.Single(refreshed).Name);
    }

    [Fact]
    public async Task GetEnabledAsync_KeysDifferentTypesIndependently()
    {
        // Arrange
        var cache = CreateCache(out _);

        // Act
        var sample = await cache.GetEnabledAsync<SampleEntry>(
            _ => Task.FromResult<IReadOnlyCollection<SampleEntry>>([new SampleEntry { Name = "sample" }]),
            TestContext.Current.CancellationToken);

        var other = await cache.GetEnabledAsync<OtherEntry>(
            _ => Task.FromResult<IReadOnlyCollection<OtherEntry>>([new OtherEntry { Name = "other" }]),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("sample", Assert.Single(sample).Name);
        Assert.Equal("other", Assert.Single(other).Name);
    }

    [Fact]
    public async Task InvalidateEnabledAsync_DoesNotAffectOtherTypes()
    {
        // Arrange
        var cache = CreateCache(out _);
        var sampleLoads = 0;
        var otherLoads = 0;

        Func<CancellationToken, Task<IReadOnlyCollection<SampleEntry>>> sampleFactory = _ =>
        {
            sampleLoads++;

            return Task.FromResult<IReadOnlyCollection<SampleEntry>>([new SampleEntry { Name = "s" }]);
        };

        Func<CancellationToken, Task<IReadOnlyCollection<OtherEntry>>> otherFactory = _ =>
        {
            otherLoads++;

            return Task.FromResult<IReadOnlyCollection<OtherEntry>>([new OtherEntry { Name = "o" }]);
        };

        // Act
        await cache.GetEnabledAsync(sampleFactory, TestContext.Current.CancellationToken);
        await cache.GetEnabledAsync(otherFactory, TestContext.Current.CancellationToken);

        await cache.InvalidateEnabledAsync<SampleEntry>();

        await cache.GetEnabledAsync(sampleFactory, TestContext.Current.CancellationToken);
        await cache.GetEnabledAsync(otherFactory, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, sampleLoads);
        Assert.Equal(1, otherLoads);
    }

    [Fact]
    public async Task GetEnabledAsync_WhenInvalidatedDuringLoad_DoesNotServeStaleSnapshot()
    {
        // Arrange
        var cache = CreateCache(out _);
        var loadCount = 0;

        // The first load is invalidated after its change token was captured but before the value is stored, simulating
        // a concurrent write that commits while the factory is running. The captured token trips, so the stored entry
        // must expire immediately and the next read must reload rather than serve the stale snapshot.
        Func<CancellationToken, Task<IReadOnlyCollection<SampleEntry>>> factory = async _ =>
        {
            loadCount++;

            if (loadCount == 1)
            {
                await cache.InvalidateEnabledAsync<SampleEntry>();
            }

            return [new SampleEntry { Name = "load-" + loadCount }];
        };

        // Act
        var first = await cache.GetEnabledAsync(factory, TestContext.Current.CancellationToken);
        var second = await cache.GetEnabledAsync(factory, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("load-1", Assert.Single(first).Name);
        Assert.Equal(2, loadCount);
        Assert.Equal("load-2", Assert.Single(second).Name);
    }
}
