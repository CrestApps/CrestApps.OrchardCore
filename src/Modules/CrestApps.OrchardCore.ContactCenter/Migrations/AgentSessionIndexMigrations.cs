using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentSessionIndex"/>.
/// </summary>
internal sealed class AgentSessionIndexMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public AgentSessionIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the agent session index table and its supporting unique user claim.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AgentSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<bool>("IsOnline")
            .Column<DateTime>("LastHeartbeatUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AgentSessionIndex>(table => table
            .CreateIndex("IDX_AgentSessionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "UserId",
                "IsOnline",
                "LastHeartbeatUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(AgentSessionIndex),
            "UQ_AgentSessionIndex_UserId",
            "UserId");

        await SchemaBuilder.AlterIndexTableAsync<AgentSessionIndex>(table => table
            .CreateIndex(
                "IDX_AgentSessionIndex_Retention",
                "LastHeartbeatUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 3;
    }
    /// <summary>
    /// Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
    /// is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<AgentSessionIndex>(table => table
            .CreateIndex(
                "IDX_AgentSessionIndex_Retention",
                "LastHeartbeatUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 3;
    }


    /// <summary>
    /// Adds the tenant-scoped unique user claim to existing agent session indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        var quotedTableName = ContactCenterMigrationSql.GetQuotedTableName(SchemaBuilder, _store, typeof(AgentSessionIndex));
        var userIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("UserId");

        var hasDuplicateUsers = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {userIdColumn} IS NOT NULL AND {userIdColumn} <> ''
            GROUP BY {userIdColumn}
            HAVING COUNT(*) > 1
            """);

        if (hasDuplicateUsers)
        {
            throw new InvalidOperationException(
                "The Contact Center agent-session index contains multiple sessions for one user. Resolve duplicate legacy agent sessions before enabling the tenant-scoped user uniqueness constraint.");
        }

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(AgentSessionIndex),
            "UQ_AgentSessionIndex_UserId",
            "UserId");

        return 2;
    }
}
