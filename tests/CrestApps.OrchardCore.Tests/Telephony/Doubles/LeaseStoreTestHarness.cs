using CrestApps.OrchardCore.Asterisk.Indexes;
using CrestApps.OrchardCore.Asterisk.Migrations;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// Builds a real in-memory YesSql <see cref="IStore"/> wired with the Asterisk PJSIP credential lease index
/// and migration so tests can exercise the REAL <c>AsteriskPjsipCredentialLeaseStore</c> and observe durable,
/// cross-session commit semantics that in-memory fakes cannot reproduce.
/// </summary>
internal static class LeaseStoreTestHarness
{
    public static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"asterisk-{prefix}-{Guid.NewGuid():N}.db");

    public static async Task<IStore> CreateStoreAsync(string databasePath, CancellationToken cancellationToken)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new AsteriskPjsipCredentialLeaseIndexProvider()]);
        await store.InitializeAsync(cancellationToken);
        await CreateIndexSchemaAsync(store, cancellationToken);

        return store;
    }

    private static async Task CreateIndexSchemaAsync(IStore store, CancellationToken cancellationToken)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var migration = new AsteriskPjsipCredentialLeaseMigrations
        {
            SchemaBuilder = schemaBuilder,
        };
        await migration.CreateAsync();
        await transaction.CommitAsync(cancellationToken);
    }
}
