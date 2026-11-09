using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Numbering;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Core.Numbering;

/// <summary>
/// The shared number sequence runs against a real store, because what matters is what the next issuer reads back after
/// the last one saved.
/// </summary>
public sealed class NumberSequenceTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-sequence-{Guid.NewGuid():N}.db");
    private readonly IDistributedLock _locks = new LocalLock(NullLogger<LocalLock>.Instance);
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));

        await _store.InitializeAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task NextAsync_IssuesNumbersInOrder_FromTheFirstNumber_PerSeries()
    {
        // Act
        var orders = new List<long>();

        for (var i = 0; i < 3; i++)
        {
            orders.Add(await NextAsync("Orders", new NumberSequenceOptions { FirstNumber = 1001 }));
        }

        var visits = await NextAsync("Visits");

        // Assert
        Assert.Equal([1001, 1002, 1003], orders);
        Assert.Equal(1, visits);
    }

    [Fact]
    public async Task NextAsync_SeedsANewSeriesFromTheHighestNumberInUse_Once()
    {
        // Arrange
        var seedCalls = 0;
        var options = new NumberSequenceOptions
        {
            SeedAsync = _ =>
            {
                seedCalls++;

                return Task.FromResult(41L);
            },
        };

        // Act
        var first = await NextAsync("WorkOrders", options);
        var second = await NextAsync("WorkOrders", options);

        // Assert
        Assert.Equal(42, first);
        Assert.Equal(43, second);
        Assert.Equal(1, seedCalls);
    }

    [Fact]
    public async Task NextAsync_NeverStartsBelowTheFirstNumber_WhenTheSeedIsLower()
    {
        // Act
        var number = await NextAsync("Returns", new NumberSequenceOptions { FirstNumber = 1001, SeedAsync = _ => Task.FromResult(7L) });

        // Assert
        Assert.Equal(1001, number);
    }

    [Fact]
    public async Task NextAsync_InOneUnitOfWork_IssuesConsecutiveNumbers()
    {
        // Arrange
        await using var session = _store.CreateSession();
        await using var sequence = new DocumentNumberSequence(session, _locks);

        // Act
        var first = await sequence.NextAsync("Batch", cancellationToken: TestContext.Current.CancellationToken);
        var second = await sequence.NextAsync("Batch", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task NextAsync_FromConcurrentUnitsOfWork_NeverIssuesANumberTwice()
    {
        // Act
        var numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => NextAsync("Concurrent"))));

        // Assert
        Assert.Equal(20, numbers.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 20).Select(number => (long)number), numbers.Order());
    }

    [Fact]
    public async Task NextAsync_WithAPrefix_FormatsTheNumberWithItsDigits()
    {
        // Arrange
        await using var session = _store.CreateSession();
        await using var numberSequence = new DocumentNumberSequence(session, _locks);
        INumberSequence sequence = numberSequence;

        // Act
        var number = await sequence.NextAsync("Visits", "V-", 6, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("V-000001", number);
    }

    private async Task<long> NextAsync(string series, NumberSequenceOptions options = null)
    {
        // Each number is issued by its own unit of work, as each order is: the session is saved, then the scope that
        // owns the sequence ends and lets the next issuer in.
        await using var session = _store.CreateSession();
        await using var sequence = new DocumentNumberSequence(session, _locks);

        var number = await sequence.NextAsync(series, options, TestContext.Current.CancellationToken);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return number;
    }
}
