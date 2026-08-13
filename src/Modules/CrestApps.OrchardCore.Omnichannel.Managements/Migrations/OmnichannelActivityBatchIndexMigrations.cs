using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using YesSql;
using YesSql.Sql;

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

        return 3;
    }

    /// <summary>
    /// Adds the activity batch source column in an isolated transaction so it survives sibling migration failures.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await using var connection = DbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        await ApplyIsolatedSchemaChangeAsync(connection,
            builder => builder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table =>
                table.AddColumn<string>("Source", column => column.WithLength(50)),
                collection: OmnichannelConstants.CollectionName),
            "add the 'Source' column to the omnichannel activity batch index");

        return 2;
    }

    /// <summary>
    /// Adds the activity batch created UTC column, used to order batches by newest first, in an isolated
    /// transaction so it survives sibling migration failures in the same feature.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await using var connection = DbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        await ApplyIsolatedSchemaChangeAsync(connection,
            builder => builder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table =>
                table.AddColumn<DateTime>("CreatedUtc"),
                collection: OmnichannelConstants.CollectionName),
            "add the 'CreatedUtc' column to the omnichannel activity batch index");

        return 3;
    }
}
