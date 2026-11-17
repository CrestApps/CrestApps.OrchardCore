using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Migrations;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
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

        // Arrange: the version 1 tables.
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

            // Act
            var version = await new AgenticCommerceMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

            await transaction.CommitAsync(cancellationToken);

            Assert.Equal(2, version);
        }

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
            await AgenticCommerceMigrations.ReindexAsync(session);
        }

        // Assert
        await using var check = _store.CreateSession();

        Assert.Equal("p1", (await new AgentPlatformStore(check).FindByProfileHostAsync("xn--bcher-kva.example", cancellationToken))?.ItemId);
        Assert.Equal(1, await new AgentCheckoutStore(check).CountOpenFromSameOriginAsync(AgentPlatformAddress.GetOriginKey(UnregisteredProfileUrl), cancellationToken));
    }

    private async Task ClearAsync(string index, string column)
    {
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var dialect = _store.Configuration.SqlDialect;
        await using var command = transaction.Connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = $"UPDATE {dialect.QuoteForTableName(AgenticCommerceStorage.CollectionName + "_" + index, _store.Configuration.Schema)} SET {dialect.QuoteForColumnName(column)} = NULL";

        Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }
}
