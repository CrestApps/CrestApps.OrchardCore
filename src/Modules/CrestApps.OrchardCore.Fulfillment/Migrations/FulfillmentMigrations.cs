using CrestApps.OrchardCore.Fulfillment.Core.Indexes;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Fulfillment.Migrations;

internal sealed class FulfillmentMigrations : DataMigration
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes the migration with the tenant's current session.
    /// </summary>
    /// <param name="session">The session that owns the migration transaction.</param>
    public FulfillmentMigrations(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Creates the fulfillment request index for a new tenant.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await FulfillmentIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    /// <summary>
    /// Upgrades tenants that enabled Fulfillment before the order number and shipment were indexed.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        const string collection = FulfillmentConstants.CollectionName;
        await IndexTableColumnCreation.AddIfMissingAsync<FulfillmentRequestIndex>(SchemaBuilder, collection, "OrderNumber",
            table => table.AddColumn<string>("OrderNumber", column => column.Nullable().WithLength(64)));
        await IndexTableColumnCreation.AddIfMissingAsync<FulfillmentRequestIndex>(SchemaBuilder, collection, "ShipmentId",
            table => table.AddColumn<string>("ShipmentId", column => column.Nullable().WithLength(26)));

        await IndexTableIndexCreation.CreateIfMissingAsync<FulfillmentRequestIndex>(SchemaBuilder,
            "IDX_FulfillmentRequestIndex_List", collection, "ModifiedUtc", "Status");

        // A retry can find the columns already present on a database that commits schema changes separately.
        await CatalogIndexBackfill.ReindexInTransactionAsync<FulfillmentRequest, FulfillmentRequestIndex>(_session, collection);

        return 2;
    }
}
