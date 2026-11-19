using CrestApps.OrchardCore.Fulfillment.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Fulfillment.Migrations;

internal sealed class FulfillmentMigrations : DataMigration
{
    /// <summary>
    /// Creates the fulfillment request index for a new tenant.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await FulfillmentIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
