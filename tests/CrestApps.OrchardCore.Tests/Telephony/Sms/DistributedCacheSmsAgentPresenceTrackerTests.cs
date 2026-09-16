using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public sealed class DistributedCacheSmsAgentPresenceTrackerTests
{
    [Fact]
    public async Task IsPresent_IsFalse_ForAnAgentWhoHasNeverCheckedIn()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var present = await harness.Tracker.IsPresentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(present);
    }

    [Fact]
    public async Task Touch_MakesTheAgentPresent_UntilTheHeartbeatTimeoutLapses()
    {
        // Arrange
        // Presence is a fact that expires on its own. The page checks in well inside the timeout while it is
        // open; a dropped tab stops checking in and simply lapses, with nothing to clean up.
        var harness = new Harness();

        // Act
        await harness.Tracker.TouchAsync("a1", TestContext.Current.CancellationToken);
        var presentAfterTouch = await harness.Tracker.IsPresentAsync("a1", TestContext.Current.CancellationToken);

        harness.Advance(TimeSpan.FromSeconds(60));
        var presentInsideTimeout = await harness.Tracker.IsPresentAsync("a1", TestContext.Current.CancellationToken);

        harness.Advance(TimeSpan.FromSeconds(60));
        var presentAfterTimeout = await harness.Tracker.IsPresentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(presentAfterTouch);
        Assert.True(presentInsideTimeout);
        Assert.False(presentAfterTimeout);
    }

    [Fact]
    public async Task Touch_KeepsAnAgentPresent_WhileTheyKeepCheckingIn()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.Tracker.TouchAsync("a1", TestContext.Current.CancellationToken);
        harness.Advance(TimeSpan.FromSeconds(60));
        await harness.Tracker.TouchAsync("a1", TestContext.Current.CancellationToken);
        harness.Advance(TimeSpan.FromSeconds(60));

        var present = await harness.Tracker.IsPresentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(present);
    }

    [Fact]
    public async Task Presence_IsKeyedByTenant_SoOneTenantNeverSeesAnothersAgents()
    {
        // Arrange
        var cache = new FakeDistributedCache();
        var first = new Harness(cache, tenantName: "Alpha");
        var second = new Harness(cache, tenantName: "Beta");

        // Act
        await first.Tracker.TouchAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(await first.Tracker.IsPresentAsync("a1", TestContext.Current.CancellationToken));
        Assert.False(await second.Tracker.IsPresentAsync("a1", TestContext.Current.CancellationToken));
    }

    private sealed class Harness
    {
        private readonly FakeDistributedCache _cache;

        public DistributedCacheSmsAgentPresenceTracker Tracker { get; }

        public Harness()
            : this(new FakeDistributedCache(), tenantName: "Default")
        {
        }

        public Harness(FakeDistributedCache cache, string tenantName)
        {
            _cache = cache;

            Tracker = new DistributedCacheSmsAgentPresenceTracker(
                cache,
                new OptionsWrapper<AgentAvailabilityOptions>(new AgentAvailabilityOptions { HeartbeatTimeout = TimeSpan.FromSeconds(90) }),
                new ShellSettings { Name = tenantName });
        }

        public void Advance(TimeSpan duration)
            => _cache.Now += duration;
    }

    /// <summary>
    /// An in-memory cache with a clock the test controls, so an expiry can be crossed without waiting for it.
    /// Only absolute-relative expiration is honoured, which is all the tracker uses.
    /// </summary>
    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, (byte[] Value, DateTimeOffset? ExpiresAt)> _entries = new(StringComparer.Ordinal);

        public DateTimeOffset Now { get; set; } = new(2026, 3, 4, 15, 0, 0, TimeSpan.Zero);

        public byte[] Get(string key)
        {
            if (_entries.TryGetValue(key, out var entry) && (entry.ExpiresAt is null || entry.ExpiresAt > Now))
            {
                return entry.Value;
            }

            _entries.Remove(key);

            return null;
        }

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            var expiresAt = options.AbsoluteExpirationRelativeToNow is { } relative
                ? Now + relative
                : options.AbsoluteExpiration;

            _entries[key] = (value, expiresAt);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);

            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;

        public void Remove(string key)
            => _entries.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);

            return Task.CompletedTask;
        }
    }
}
