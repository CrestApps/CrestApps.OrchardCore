using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Migrations;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// The agentic commerce migration's version 2: a site made with version 1 gets the profile host and origin columns, and
/// the platforms and checkouts saved before them are indexed again, so they are found by host and counted by origin.
/// </summary>
public sealed class AgenticCommerceIndexUpgradeTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-agentic-upgrade-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new AgentPlatformIndexProvider(), new AgentCheckoutIndexProvider()], AgenticCommerceStorage.CollectionName);
        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(AgenticCommerceStorage.CollectionName, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AVersion1Site_IsUpgraded_AndItsRecordsAreFoundByHostAndOrigin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string ProfileUrl = "https://Bücher.Example/.well-known/ucp";
        const string UnregisteredProfileUrl = "https://agent.example/a";

        await UpgradeFromVersion1Async();

        // Records indexed before the upgrade have no host or origin yet.
        await using (var session = _store.CreateSession())
        {
            var checkout = new AgentCheckout
            {
                ItemId = "c1",
                PlatformId = UcpProfileClient.GetUnregisteredPlatformId(UnregisteredProfileUrl),
                Protocol = AgenticCommerceConstants.Protocols.Ucp,
                Status = AgentCheckoutStatus.Incomplete,
            };

            checkout.SetProperty(AgentCheckout.ProfileUrlProperty, UnregisteredProfileUrl);
            await new AgentPlatformStore(session).CreateAsync(new AgentPlatform { ItemId = "p1", Name = "Agent", ProfileUrl = ProfileUrl }, cancellationToken);
            await new AgentCheckoutStore(session).CreateAsync(checkout, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        await ClearAsync("AgentPlatformIndex", "ProfileHost");
        await ClearAsync("AgentCheckoutIndex", "PlatformOrigin");

        // Act: the deferred task the upgrade schedules.
        await using (var session = _store.CreateSession())
        {
            await AgenticCommerceMigrations.ReindexAsync(session, NullLogger.Instance);
        }

        // Assert
        await using var check = _store.CreateSession();

        Assert.Equal("p1", (await new AgentPlatformStore(check).FindByProfileHostAsync("xn--bcher-kva.example", cancellationToken))?.ItemId);
        Assert.Equal(1, await new AgentCheckoutStore(check).CountOpenFromSameOriginAsync(AgentPlatformAddress.GetOriginKey(UnregisteredProfileUrl), cancellationToken));
    }

    /// <summary>
    /// G14 (fix verification): a platform saved before the upgrade with a Unicode host whose punycode form is too long made
    /// <see cref="Uri.IdnHost"/> throw, so it could not be saved again (a 500 in the editor) and its page of the deferred
    /// re-index failed, which left every platform after it without a host. It is now saved with no host, and the
    /// platforms after it are indexed.
    /// </summary>
    [Fact]
    public async Task APlatformWhoseHostCannotBeIndexed_IsSaved_AndTheOthersAreStillIndexed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var unindexable = "https://" + string.Join('.', Enumerable.Repeat(new string('ü', 50), 5)) + ".example/.well-known/ucp";

        await UpgradeFromVersion1Async();

        await using (var session = _store.CreateSession())
        {
            var platforms = new AgentPlatformStore(session);

            await platforms.CreateAsync(new AgentPlatform { ItemId = "p0", Name = "Long", ProfileUrl = unindexable }, cancellationToken);
            await platforms.CreateAsync(new AgentPlatform { ItemId = "p1", Name = "Agent", ProfileUrl = "https://agent.example/.well-known/ucp" }, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        await ClearAsync("AgentPlatformIndex", "ProfileHost");

        await using (var session = _store.CreateSession())
        {
            await AgenticCommerceMigrations.ReindexAsync(session, NullLogger.Instance);
        }

        await using var check = _store.CreateSession();

        Assert.Equal("p1", (await new AgentPlatformStore(check).FindByProfileHostAsync("agent.example", cancellationToken))?.ItemId);
    }

    /// <summary>
    /// G14 (fix verification): one record that fails to be mapped again no longer stops the backfill. Its page is saved
    /// again one record at a time, the failing record is skipped and logged, and the records after it are indexed.
    /// </summary>
    [Fact]
    public async Task ARecordThatFailsToBeMappedAgain_IsSkipped_AndTheRecordsAfterItAreIndexed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var broken = new BrokenPlatformIndexProvider();

        _store.RegisterIndexes([broken], AgenticCommerceStorage.CollectionName);
        await UpgradeFromVersion1Async();

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            await new SchemaBuilder(_store.Configuration, transaction).CreateMapIndexTableAsync<BrokenPlatformIndex>(table => table
                .Column<string>("Name", column => column.WithLength(255)),
                collection: AgenticCommerceStorage.CollectionName);
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            var platforms = new AgentPlatformStore(session);

            await platforms.CreateAsync(new AgentPlatform { ItemId = "p0", Name = "Agent 0", ProfileUrl = "https://a0.example/p" }, cancellationToken);
            await platforms.CreateAsync(new AgentPlatform { ItemId = "p1", Name = BrokenPlatformIndexProvider.BrokenName, ProfileUrl = "https://a1.example/p" }, cancellationToken);
            await platforms.CreateAsync(new AgentPlatform { ItemId = "p2", Name = "Agent 2", ProfileUrl = "https://a2.example/p" }, cancellationToken);
            await platforms.CreateAsync(new AgentPlatform { ItemId = "p3", Name = "Agent 3", ProfileUrl = "https://a3.example/p" }, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        await ClearAsync("AgentPlatformIndex", "ProfileHost");

        var logger = new RecordingLogger<AgenticCommerceIndexUpgradeTests>();
        int saved;

        broken.Enabled = true;

        await using (var session = _store.CreateSession())
        {
            saved = await CatalogIndexBackfill.ReindexAsync<AgentPlatform, AgentPlatformIndex>(session, AgenticCommerceStorage.CollectionName, pageSize: 2, logger);
        }

        await using var check = _store.CreateSession();
        var store = new AgentPlatformStore(check);

        Assert.Equal(3, saved);
        Assert.Equal("p0", (await store.FindByProfileHostAsync("a0.example", cancellationToken))?.ItemId);
        Assert.Null(await store.FindByProfileHostAsync("a1.example", cancellationToken));
        Assert.Equal("p2", (await store.FindByProfileHostAsync("a2.example", cancellationToken))?.ItemId);
        Assert.Equal("p3", (await store.FindByProfileHostAsync("a3.example", cancellationToken))?.ItemId);
        Assert.Contains(logger.At(LogLevel.Warning), message => message.Contains("p1", StringComparison.Ordinal));
    }

    // Creates the version 1 tables and runs the upgrade to version 2.
    private async Task UpgradeFromVersion1Async()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await schemaBuilder.CreateMapIndexTableAsync<AgentPlatformIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("Name", column => column.WithLength(255))
                .Column<bool>("Enabled")
                .Column<string>("ApiKeyHash", column => column.WithLength(64))
                .Column<string>("OAuthClientId", column => column.WithLength(255))
                .Column<string>("ProfileUrl", column => column.WithLength(1000)),
                collection: AgenticCommerceStorage.CollectionName);
            await schemaBuilder.CreateMapIndexTableAsync<AgentCheckoutIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("PlatformId", column => column.WithLength(26))
                .Column<string>("Protocol", column => column.WithLength(20))
                .Column<bool>("IsTerminal")
                .Column<string>("CheckoutSessionId", column => column.WithLength(26))
                .Column<string>("OrderId", column => column.WithLength(26))
                .Column<DateTime>("ExpiresUtc"),
                collection: AgenticCommerceStorage.CollectionName);

            var version = await new AgenticCommerceMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

            await transaction.CommitAsync(cancellationToken);

            Assert.Equal(2, version);
        }
    }

    private async Task ClearAsync(string index, string column)
    {
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var dialect = _store.Configuration.SqlDialect;
        await using var command = transaction.Connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = $"UPDATE {dialect.QuoteForTableName(AgenticCommerceStorage.CollectionName + "_" + index, _store.Configuration.Schema)} SET {dialect.QuoteForColumnName(column)} = NULL";

        Assert.True(await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken) > 0);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    // An index whose map fails, once enabled, for the platform with a given name: a record the backfill cannot map again.
    private sealed class BrokenPlatformIndexProvider : IndexProvider<AgentPlatform>
    {
        public const string BrokenName = "Broken";

        public BrokenPlatformIndexProvider()
        {
            CollectionName = AgenticCommerceStorage.CollectionName;
        }

        public bool Enabled { get; set; }

        public override void Describe(DescribeContext<AgentPlatform> context)
            => context
                .For<BrokenPlatformIndex>()
                .Map(platform => Enabled && platform.Name == BrokenName
                    ? throw new InvalidOperationException($"Platform {platform.ItemId} cannot be mapped.")
                    : new BrokenPlatformIndex { Name = platform.Name });
    }

    private sealed class BrokenPlatformIndex : MapIndex
    {
        public string Name { get; set; }
    }
}
