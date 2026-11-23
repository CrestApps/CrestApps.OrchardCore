using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Core.Indexes;
using CrestApps.OrchardCore.Consent.Core.Models;
using CrestApps.OrchardCore.Consent.Core.Services;
using CrestApps.OrchardCore.Consent.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Consent;

/// <summary>
/// The address opt-outs on a real SQLite store, with the module's migration and index: one record per channel,
/// address and purpose, found whatever case the address is written in, all removed by a delete, and read a page at a
/// time for an export.
/// </summary>
public sealed class ConsentAddressOptOutStoreTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"consent-opt-outs-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ConsentAddressOptOutIndexProvider()]);
        await _store.InitializeAsync(cancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        Assert.Equal(1, await new ConsentAddressOptOutMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync());

        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SaveAsync_TheSameOptOutTwice_KeepsOneRecord_WithTheLatestSourceAndTime()
    {
        // Arrange
        await SaveAsync(Record("Sam@Example.com ", ConsentPurposes.Marketing, "unsubscribe-link", _now));

        // Act
        await SaveAsync(Record("sam@example.com", ConsentPurposes.Marketing, "MarketingUnsubscribe", _now.AddDays(1)));
        await SaveAsync(Record("sam@example.com", "Surveys", "unsubscribe-link", _now));

        // Assert
        await using var session = _store.CreateSession();
        var store = new ConsentAddressOptOutStore(session);
        var records = await store.ListAsync(ConsentChannels.Email, "SAM@example.com", TestContext.Current.CancellationToken);
        var marketing = Assert.Single(records, record => record.Purpose == ConsentPurposes.Marketing);

        Assert.Equal(2, records.Count);
        Assert.Equal("sam@example.com", marketing.Address);
        Assert.Equal("MarketingUnsubscribe", marketing.Source);
        Assert.Equal(_now.AddDays(1), DateTime.SpecifyKind(marketing.OptedOutUtc, DateTimeKind.Utc));
        Assert.True(await store.ExistsAsync(ConsentChannels.Email, " SAM@EXAMPLE.COM", ConsentPurposes.Marketing, TestContext.Current.CancellationToken));
        Assert.False(await store.ExistsAsync(ConsentChannels.Email, "sam@example.com", null, TestContext.Current.CancellationToken));
        Assert.False(await store.ExistsAsync(ConsentChannels.Sms, "sam@example.com", ConsentPurposes.Marketing, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_RemovesEveryRecordOfThePurpose_EvenDuplicates()
    {
        // Arrange
        await using (var session = _store.CreateSession())
        {
            // Two opt-outs recorded at the same moment can each have added a record.
            await session.SaveAsync(new ConsentAddressOptOut { ItemId = "a", Channel = ConsentChannels.Email, Address = "sam@example.com", Purpose = ConsentPurposes.Marketing, OptedOutUtc = _now }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(new ConsentAddressOptOut { ItemId = "b", Channel = ConsentChannels.Email, Address = "sam@example.com", Purpose = ConsentPurposes.Marketing, OptedOutUtc = _now }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(new ConsentAddressOptOut { ItemId = "c", Channel = ConsentChannels.Email, Address = "sam@example.com", Purpose = "Surveys", OptedOutUtc = _now }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        int deleted;

        // Act
        await using (var session = _store.CreateSession())
        {
            deleted = await new ConsentAddressOptOutStore(session).DeleteAsync(ConsentChannels.Email, "Sam@Example.com", ConsentPurposes.Marketing, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        await using var check = _store.CreateSession();
        var left = Assert.Single(await new ConsentAddressOptOutStore(check).ListAsync(ConsentChannels.Email, "sam@example.com", TestContext.Current.CancellationToken));

        Assert.Equal(2, deleted);
        Assert.Equal("Surveys", left.Purpose);
    }

    [Fact]
    public async Task PageAsync_ReadsEveryRecordOnce_APageAtATime()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            await SaveAsync(Record($"person{i}@example.com", ConsentPurposes.Marketing, "unsubscribe-link", _now));
        }

        var read = new List<string>();
        var pages = 0;
        long afterId = 0;

        // Act
        await using var session = _store.CreateSession();
        var store = new ConsentAddressOptOutStore(session);

        do
        {
            var (records, nextId) = await store.PageAsync(afterId, 2, TestContext.Current.CancellationToken);
            read.AddRange(records.Select(record => record.Address));
            afterId = nextId;
            pages++;
        }
        while (afterId > 0);

        // Assert
        Assert.Equal(3, pages);
        Assert.Equal(Enumerable.Range(0, 5).Select(i => $"person{i}@example.com"), read);
    }

    private async Task SaveAsync(ConsentAddressOptOut optOut)
    {
        await using var session = _store.CreateSession();
        await new ConsentAddressOptOutStore(session).SaveAsync(optOut, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ConsentAddressOptOut Record(string address, string purpose, string source, DateTime optedOutUtc)
        => new()
        {
            Channel = ConsentChannels.Email,
            Address = address,
            Purpose = purpose,
            Source = source,
            OptedOutUtc = optedOutUtc,
        };
}
