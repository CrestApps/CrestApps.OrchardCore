using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Scheduling.Migrations;

internal sealed class SchedulingMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchedulingIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
