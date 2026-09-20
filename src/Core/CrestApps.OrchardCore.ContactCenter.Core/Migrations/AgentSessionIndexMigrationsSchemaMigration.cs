using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentSessionIndex"/>.
/// </summary>
internal sealed class AgentSessionIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public AgentSessionIndexMigrationsSchemaMigration(IStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "AgentSessionIndexMigrations";

    /// <summary>
    /// Creates the agent session index table and its supporting unique user claim.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<AgentSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<bool>("IsOnline")
            .Column<DateTime>("LastHeartbeatUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<AgentSessionIndex>(table => table
            .CreateIndex("IDX_AgentSessionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "UserId",
                "IsOnline",
                "LastHeartbeatUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
            _store,
            typeof(AgentSessionIndex),
            "UQ_AgentSessionIndex_UserId",
            "UserId");

        await builder.AlterIndexTableAsync<AgentSessionIndex>(table => table
            .CreateIndex(
                "IDX_AgentSessionIndex_Retention",
                "LastHeartbeatUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 3;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                return await UpdateFrom1Async(builder);

            case 2:
                return await UpdateFrom2Async(builder);

            default:
                return version;
        }
    }

    /// <summary>
    /// Moves the schema from version 1 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, matching the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method: folding every version into one switch would make
    /// one authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
                    // Adds the tenant-scoped unique user claim to existing agent session indexes.
                    var quotedTableName = ContactCenterMigrationSql.GetQuotedTableName(builder, _store, typeof(AgentSessionIndex));
                    var userIdColumn = builder.Dialect.QuoteForColumnName("UserId");

                    var hasDuplicateUsers = await ContactCenterMigrationSql.ExistsAsync(
                        builder,
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
                        builder,
                        _store,
                        typeof(AgentSessionIndex),
                        "UQ_AgentSessionIndex_UserId",
                        "UserId");

                    return 2;
                }

    /// <summary>
    /// Moves the schema from version 2 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, matching the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method: folding every version into one switch would make
    /// one authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
                    // Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
                    // is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
                    await builder.AlterIndexTableAsync<AgentSessionIndex>(table => table
                        .CreateIndex(
                            "IDX_AgentSessionIndex_Retention",
                            "LastHeartbeatUtc",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 3;
                }
}
