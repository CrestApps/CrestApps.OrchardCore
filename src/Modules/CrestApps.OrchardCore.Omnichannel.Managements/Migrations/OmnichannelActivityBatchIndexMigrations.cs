using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using YesSql.Sql;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class OmnichannelActivityBatchIndexMigrations : OmnichannelIndexMigration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityBatchIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelActivityBatchIndexMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<OmnichannelActivityBatchIndexMigrations> logger)
        : base(store, dbConnectionAccessor, logger)
    {
    }

    /// <summary>
    /// Creates the omnichannel activity batch index table with the final set of columns and indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<string>("Source", column => column.WithLength(50))
            .Column<OmnichannelActivityBatchStatus>("Status")
            .Column<DateTime>("CreatedUtc"),
        collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating incoming message from Omnichannel (Incoming SMS, Email, etc).
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityBatchIndex_DocumentId",
        "DocumentId",
        "DisplayText",
        "ItemId"
        ),
        collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelActivityBatchIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);

        await CreateReportIndexAsync();

        return 6;
    }

    /// <summary>
    /// Adds the activity batch source column in isolation so it survives sibling migration failures.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table =>
                table.AddColumn<string>("Source", column => column.WithLength(50)),
                collection: OmnichannelConstants.CollectionName),
            "add the 'Source' column to the omnichannel activity batch index");

        return 2;
    }

    /// <summary>
    /// Adds the activity batch created UTC column, used to order batches by newest first, in isolation so
    /// it survives sibling migration failures in the same feature.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table =>
                table.AddColumn<DateTime>("CreatedUtc"),
                collection: OmnichannelConstants.CollectionName),
            "add the 'CreatedUtc' column to the omnichannel activity batch index");

        return 3;
    }

    /// <summary>
    /// Repairs databases whose migration version was recorded at or above the step that added the
    /// <c>Source</c> and <c>CreatedUtc</c> columns while the physical columns were rolled back with a failed
    /// sibling migration. Because the earlier version-gated steps no longer run on those databases, this
    /// step verifies each column and adds only the ones that are missing, so the activity batches screen can
    /// order by <c>CreatedUtc</c> again.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await EnsureColumnExistsAsync<OmnichannelActivityBatchIndex>(
            OmnichannelConstants.CollectionName,
            "Source",
            table => table.AddColumn<string>("Source", column => column.WithLength(50)),
            "ensure the 'Source' column exists on the omnichannel activity batch index");

        await EnsureColumnExistsAsync<OmnichannelActivityBatchIndex>(
            OmnichannelConstants.CollectionName,
            "CreatedUtc",
            table => table.AddColumn<DateTime>("CreatedUtc"),
            "ensure the 'CreatedUtc' column exists on the omnichannel activity batch index");

        return 4;
    }

    /// <summary>
    /// Adds the index a record is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelActivityBatchIndex>(
            SchemaBuilder,
            "IDX_OmnichannelActivityBatchIndex_ItemId",
            OmnichannelConstants.CollectionName,
            "ItemId", "DocumentId");

        return 5;
    }

    /// <summary>
    /// Adds the index reports read the records through, newest first by <c>CreatedUtc</c> a page at a time, so
    /// each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        await CreateReportIndexAsync();

        return 6;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelActivityBatchIndex>(
            SchemaBuilder,
            "IDX_OmnichannelActivityBatchIndex_Report",
            OmnichannelConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
