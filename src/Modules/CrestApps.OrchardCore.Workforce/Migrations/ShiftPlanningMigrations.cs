using CrestApps.OrchardCore.Workforce.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class ShiftPlanningMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await ShiftPlanningIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await ShiftPlanningIndexSchema.CreateMarketplaceTablesAsync(SchemaBuilder);

        return 2;
    }
}
