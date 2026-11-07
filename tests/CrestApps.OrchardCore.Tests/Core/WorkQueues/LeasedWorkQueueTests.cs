using System.Text.Json;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.WorkQueues;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Core.WorkQueues;

/// <summary>
/// The leased work queue on YesSql: one owner per claim, an expired lease is claimed again, a failure is retried after
/// its back-off and dead-lettered when its attempts run out, and a late settlement from a lost claim is refused.
/// </summary>
public sealed class LeasedWorkQueueTests : IAsyncLifetime
{
    private const string Collection = "WorkQueueTests";

    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(5);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"work-queue-{Guid.NewGuid():N}.db");
    private readonly MutableClock _clock = new();
    private IStore _store;
    private ServiceProvider _provider;

    public async ValueTask InitializeAsync()
    {
        _store = await CreateStoreAsync(_databasePath);

        var services = new ServiceCollection();

        services.AddSingleton(_store);
        services.AddScoped(_ => _store.CreateSession());
        services.AddSingleton<IClock>(_clock);
        services.AddLeasedWorkQueue<TestWorkItem>(options =>
        {
            options.CollectionName = Collection;
            options.MaxAttempts = 3;
        });

        _provider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);
    }

    [Fact]
    public async Task ClaimDue_ClaimsDueItemsOldestFirst_WithANewOwnerAndFence()
    {
        // Arrange
        await EnqueueAsync("late", _clock.UtcNow.AddMinutes(-1));
        await EnqueueAsync("early", _clock.UtcNow.AddMinutes(-10));
        await EnqueueAsync("future", _clock.UtcNow.AddMinutes(10));

        // Act
        var claimed = await WithQueueAsync(queue => queue.ClaimDueAsync(10, _lease, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["early", "late"], claimed.Select(item => item.ItemId));
        Assert.All(claimed, item =>
        {
            Assert.Equal(LeasedWorkItemStatus.Claimed, item.Status);
            Assert.False(string.IsNullOrEmpty(item.OwnerToken));
            Assert.Equal(1, item.FenceToken);
            Assert.Equal(_clock.UtcNow.Add(_lease), item.NextAttemptUtc);
        });

        var stored = await LoadAsync("early");
        Assert.Equal(LeasedWorkItemStatus.Claimed, stored.Status);
        Assert.Equal(claimed[0].OwnerToken, stored.OwnerToken);
    }

    [Fact]
    public async Task TryClaim_UnderConcurrency_HasExactlyOneOwner()
    {
        // Arrange
        await EnqueueAsync("contested", _clock.UtcNow);

        // Act
        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            WithQueueAsync(queue => queue.TryClaimAsync("contested", _lease, TestContext.Current.CancellationToken)))));

        // Assert
        var winner = Assert.Single(attempts, item => item is not null);
        Assert.Equal(1, winner.FenceToken);
        Assert.Equal(winner.OwnerToken, (await LoadAsync("contested")).OwnerToken);
    }

    [Fact]
    public async Task ClaimDue_FromSeveralWorkersAtOnce_NeverHandsOutAnItemTwice()
    {
        // Arrange
        for (var index = 0; index < 12; index++)
        {
            await EnqueueAsync($"item-{index:00}", _clock.UtcNow.AddSeconds(-index));
        }

        // Act
        var batches = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            WithQueueAsync(queue => queue.ClaimDueAsync(12, _lease, TestContext.Current.CancellationToken)))));

        // Assert
        var claimedIds = batches.SelectMany(batch => batch.Select(item => item.ItemId)).ToArray();
        Assert.Equal(12, claimedIds.Length);
        Assert.Equal(12, claimedIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task AnExpiredLease_IsClaimedAgain_AndTheLateSettlementIsRefused()
    {
        // Arrange
        await EnqueueAsync("stalled", _clock.UtcNow);
        var first = await WithQueueAsync(queue => queue.TryClaimAsync("stalled", _lease, TestContext.Current.CancellationToken));
        var beforeExpiry = await WithQueueAsync(queue => queue.ClaimDueAsync(10, _lease, TestContext.Current.CancellationToken));
        _clock.UtcNow = _clock.UtcNow.Add(_lease).AddSeconds(1);

        // Act
        var second = await WithQueueAsync(queue => queue.TryClaimAsync("stalled", _lease, TestContext.Current.CancellationToken));
        var lateCompletion = await WithQueueAsync(queue => queue.CompleteAsync(first, cancellationToken: TestContext.Current.CancellationToken));
        var lateRetry = await WithQueueAsync(queue => queue.RetryAsync(first, "late", _ => TimeSpan.FromSeconds(1), cancellationToken: TestContext.Current.CancellationToken));
        var completion = await WithQueueAsync(queue => queue.CompleteAsync(second, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(beforeExpiry);
        Assert.NotNull(second);
        Assert.Equal(2, second.FenceToken);
        Assert.NotEqual(first.OwnerToken, second.OwnerToken);
        Assert.False(lateCompletion);
        Assert.Null(lateRetry);
        Assert.True(completion);
    }

    [Fact]
    public async Task Retry_ReschedulesAfterTheBackoff_AndDeadLettersWhenAttemptsRunOut()
    {
        // Arrange
        await EnqueueAsync("flaky", _clock.UtcNow);
        var backoff = TimeSpan.FromSeconds(30);

        // Act
        var claim = await WithQueueAsync(queue => queue.TryClaimAsync("flaky", _lease, TestContext.Current.CancellationToken));
        var firstRetry = await WithQueueAsync(queue => queue.RetryAsync(claim, "boom", _ => backoff, cancellationToken: TestContext.Current.CancellationToken));
        var stored = await LoadAsync("flaky");
        var tooSoon = await WithQueueAsync(queue => queue.TryClaimAsync("flaky", _lease, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(LeasedWorkItemStatus.Pending, firstRetry);
        Assert.Equal(LeasedWorkItemStatus.Pending, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Equal("boom", stored.LastError);
        Assert.Null(stored.OwnerToken);
        Assert.Equal(_clock.UtcNow.Add(backoff), stored.NextAttemptUtc);
        Assert.Null(tooSoon);

        // Two more failures use up the queue's three attempts.
        LeasedWorkItemStatus? lastRetry = null;

        for (var attempt = 2; attempt <= 3; attempt++)
        {
            _clock.UtcNow = _clock.UtcNow.Add(backoff);
            var again = await WithQueueAsync(queue => queue.TryClaimAsync("flaky", _lease, TestContext.Current.CancellationToken));
            Assert.NotNull(again);
            lastRetry = await WithQueueAsync(queue => queue.RetryAsync(again, "boom", _ => backoff, cancellationToken: TestContext.Current.CancellationToken));
        }

        Assert.Equal(LeasedWorkItemStatus.DeadLettered, lastRetry);
        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        Assert.Null(await WithQueueAsync(queue => queue.TryClaimAsync("flaky", _lease, TestContext.Current.CancellationToken)));
        Assert.Equal(3, (await LoadAsync("flaky")).AttemptCount);
    }

    [Fact]
    public async Task Retry_ThatDoesNotCountAnAttempt_KeepsTheAttemptCount()
    {
        // Arrange
        await EnqueueAsync("waiting", _clock.UtcNow);
        var claim = await WithQueueAsync(queue => queue.TryClaimAsync("waiting", _lease, TestContext.Current.CancellationToken));

        // Act
        var status = await WithQueueAsync(queue => queue.RetryAsync(
            claim,
            "HandlerUnavailable",
            _ => TimeSpan.FromMinutes(5),
            countAttempt: false,
            update: item => item.Payload = "deferred",
            cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        var stored = await LoadAsync("waiting");
        Assert.Equal(LeasedWorkItemStatus.Pending, status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.Equal("deferred", stored.Payload);
    }

    [Fact]
    public async Task Complete_KeepsTheItemAsCompleted_AndAppliesTheUpdate()
    {
        // Arrange
        await EnqueueAsync("done", _clock.UtcNow);
        var claim = await WithQueueAsync(queue => queue.TryClaimAsync("done", _lease, TestContext.Current.CancellationToken));

        // Act
        var completed = await WithQueueAsync(queue => queue.CompleteAsync(claim, item => item.Payload = null, TestContext.Current.CancellationToken));

        // Assert
        var stored = await LoadAsync("done");
        Assert.True(completed);
        Assert.Equal(LeasedWorkItemStatus.Completed, stored.Status);
        Assert.Null(stored.OwnerToken);
        Assert.Null(stored.Payload);
        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        Assert.Empty(await WithQueueAsync(queue => queue.ClaimDueAsync(10, _lease, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Complete_OnAQueueThatRemovesCompletedItems_DeletesTheItem()
    {
        // Arrange
        await using var provider = BuildProvider(options =>
        {
            options.CollectionName = Collection;
            options.RemoveCompletedItems = true;
        });

        await EnqueueAsync("removed", _clock.UtcNow);
        var claim = await WithQueueAsync(provider, queue => queue.TryClaimAsync("removed", _lease, TestContext.Current.CancellationToken));

        // Act
        var completed = await WithQueueAsync(provider, queue => queue.CompleteAsync(claim, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.True(completed);
        Assert.Null(await LoadAsync("removed"));
    }

    [Fact]
    public async Task Abandon_DeadLettersTheItemWithTheReason()
    {
        // Arrange
        await EnqueueAsync("poison", _clock.UtcNow);
        var claim = await WithQueueAsync(queue => queue.TryClaimAsync("poison", _lease, TestContext.Current.CancellationToken));

        // Act
        var abandoned = await WithQueueAsync(queue => queue.AbandonAsync(claim, "The referenced event no longer exists.", cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        var stored = await LoadAsync("poison");
        Assert.True(abandoned);
        Assert.Equal(LeasedWorkItemStatus.DeadLettered, stored.Status);
        Assert.Equal("The referenced event no longer exists.", stored.LastError);
    }

    [Fact]
    public async Task Retry_WithAnExponentialPolicy_WaitsLongerAfterEachFailure()
    {
        // Arrange
        await EnqueueAsync("backing-off", _clock.UtcNow);
        var policy = LeasedWorkBackoff.Exponential(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30));
        var waits = new List<TimeSpan>();

        // Act
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var claim = await WithQueueAsync(queue => queue.TryClaimAsync("backing-off", _lease, TestContext.Current.CancellationToken));
            var failedAt = _clock.UtcNow;

            await WithQueueAsync(queue => queue.RetryAsync(claim, "boom", policy, cancellationToken: TestContext.Current.CancellationToken));

            var stored = await LoadAsync("backing-off");
            waits.Add(stored.NextAttemptUtc - failedAt);
            _clock.UtcNow = stored.NextAttemptUtc;
        }

        // Assert
        Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)], waits);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(4, 240)]
    [InlineData(20, 1800)]
    public void ExponentialBackoff_DoublesUpToTheCap(int attempt, int expectedSeconds)
    {
        // Act
        var backoff = LeasedWorkBackoff.Exponential(attempt, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), backoff);
    }

    private async Task EnqueueAsync(string itemId, DateTime dueUtc)
    {
        await using var scope = _provider.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<ILeasedWorkQueue<TestWorkItem>>();

        await queue.EnqueueAsync(new TestWorkItem { ItemId = itemId, NextAttemptUtc = dueUtc, Payload = itemId }, TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<TResult> WithQueueAsync<TResult>(Func<ILeasedWorkQueue<TestWorkItem>, Task<TResult>> action)
        => WithQueueAsync(_provider, action);

    private static async Task<TResult> WithQueueAsync<TResult>(IServiceProvider provider, Func<ILeasedWorkQueue<TestWorkItem>, Task<TResult>> action)
    {
        await using var scope = provider.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<ILeasedWorkQueue<TestWorkItem>>());
    }

    private async Task<TestWorkItem> LoadAsync(string itemId)
    {
        await using var session = _store.CreateSession();

        return await session.Query<TestWorkItem, LeasedWorkItemIndex>(index => index.ItemId == itemId, collection: Collection)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    private ServiceProvider BuildProvider(Action<LeasedWorkQueueOptions> configure)
    {
        var services = new ServiceCollection();

        services.AddSingleton(_store);
        services.AddScoped(_ => _store.CreateSession());
        services.AddSingleton<IClock>(_clock);
        services.AddLeasedWorkQueue<TestWorkItem>(configure);

        return services.BuildServiceProvider();
    }

    internal static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");
            configuration.ContentSerializer = new global::YesSql.Serialization.DefaultContentJsonSerializer(JOptions.Default);
        });

        store.RegisterIndexes(new IIndexProvider[] { new LeasedWorkItemIndexProvider<TestWorkItem>(Collection) });
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(Collection, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await LeasedWorkItemIndexSchema.CreateAsync(new SchemaBuilder(store.Configuration, transaction), Collection);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    public sealed class TestWorkItem : CatalogItem, ILeasedWorkItem
    {
        public string Payload { get; set; }

        public LeasedWorkItemStatus Status { get; set; }

        public string OwnerToken { get; set; }

        public long FenceToken { get; set; }

        public int AttemptCount { get; set; }

        public DateTime NextAttemptUtc { get; set; }

        public string LastError { get; set; }
    }

    private sealed class MutableClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        public ITimeZone[] GetTimeZones() => [];

        public ITimeZone GetTimeZone(string timeZoneId) => null;

        public ITimeZone GetSystemTimeZone() => null;

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;
    }
}
