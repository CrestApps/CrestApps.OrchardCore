using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.TimeTracker.Migrations;

internal sealed class TimeTrackerMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await TimeTrackerIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
