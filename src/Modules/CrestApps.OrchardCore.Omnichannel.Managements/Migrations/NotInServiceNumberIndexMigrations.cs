using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class NotInServiceNumberIndexMigrations : OmnichannelIndexMigration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotInServiceNumberIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public NotInServiceNumberIndexMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<NotInServiceNumberIndexMigrations> logger)
        : base(store, dbConnectionAccessor, logger)
    {
    }

    /// <summary>
    /// Creates the index of numbers that are not in service.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<NotInServiceNumberIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PhoneNumber", column => column.WithLength(30))
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<DateTime>("LastDetectedUtc"),
        collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<NotInServiceNumberIndex>(table => table
            .CreateIndex("IDX_NotInServiceNumberIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "PhoneNumber"
            ),
        collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<NotInServiceNumberIndex>(table => table
            .CreateIndex("IDX_NotInServiceNumberIndex_PhoneNumber",
                "PhoneNumber"
            ),
        collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<NotInServiceNumberIndex>(table => table
            .CreateIndex("IDX_NotInServiceNumberIndex_LastDetectedUtc",
                "LastDetectedUtc"
            ),
        collection: OmnichannelConstants.CollectionName
        );

        return 1;
    }
}
