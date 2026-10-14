using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class CadenceIndexMigrations : OmnichannelIndexMigration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public CadenceIndexMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<CadenceIndexMigrations> logger)
        : base(store, dbConnectionAccessor, logger)
    {
    }

    /// <summary>
    /// Creates the cadence index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CadenceIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<bool>("Enabled")
            .Column<DateTime>("CreatedUtc"),
        collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CadenceIndex>(table => table
            .CreateIndex("IDX_CadenceIndex_DocumentId",
                "DocumentId",
                "DisplayText",
                "ItemId"
            ),
        collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CadenceIndex>(table => table
            .CreateIndex(
                "IDX_CadenceIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);

        return 2;
    }

    /// <summary>
    /// Adds the index a record is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<CadenceIndex>(table => table
            .CreateIndex(
                "IDX_CadenceIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);

        return 2;
    }
}
