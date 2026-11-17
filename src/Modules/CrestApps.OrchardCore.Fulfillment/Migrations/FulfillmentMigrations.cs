using CrestApps.OrchardCore.Fulfillment.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Fulfillment.Migrations;

internal sealed class FulfillmentMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await FulfillmentIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
