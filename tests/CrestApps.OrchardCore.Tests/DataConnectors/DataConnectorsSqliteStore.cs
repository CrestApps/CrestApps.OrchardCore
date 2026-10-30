using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Indexes;
using CrestApps.OrchardCore.DataConnectors.Migrations;
using CrestApps.OrchardCore.DataConnectors.Storage;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

/// <summary>
/// A real SQLite database with the module's migration run, under a table prefix as a tenant has, so the indexes, the
/// row table and its queries are exercised the way a site runs them.
/// </summary>
internal sealed class DataConnectorsSqliteStore : IAsyncDisposable
{
    public const string TablePrefix = "tenant1_";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-connectors-{Guid.NewGuid():N}.db");

    private DataConnectorsSqliteStore()
    {
    }

    public IStore Store { get; private set; }

    public string ConnectionString => $"Data Source={_path};Pooling=False";

    public SqlDataSetRowStore CreateRowStore()
        => new(() => new SqliteConnection(ConnectionString), Store.Configuration.SqlDialect, Store.Configuration.TablePrefix, Store.Configuration.Schema);

    public static async Task<DataConnectorsSqliteStore> CreateAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new DataConnectorsSqliteStore();

        fixture.Store = StoreFactory.Create(configuration => configuration.UseSqLite(fixture.ConnectionString).SetTablePrefix(TablePrefix));
        fixture.Store.RegisterIndexes(
            [new DataConnectionIndexProvider(), new ConnectedDataSetIndexProvider(), new DataSetRefreshRunIndexProvider()],
            DataConnectorConstants.CollectionName);

        await fixture.Store.InitializeAsync(cancellationToken);
        await fixture.Store.InitializeCollectionAsync(DataConnectorConstants.CollectionName, cancellationToken);

        await using (var session = fixture.Store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var migration = new DataConnectorsMigrations { SchemaBuilder = new SchemaBuilder(fixture.Store.Configuration, transaction) };

            await migration.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        return fixture;
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(Store, _path);

        return ValueTask.CompletedTask;
    }
}
