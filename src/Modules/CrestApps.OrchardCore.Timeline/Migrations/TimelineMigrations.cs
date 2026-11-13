using CrestApps.OrchardCore.Timeline.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Timeline.Migrations;

/// <summary>
/// Creates the index table of timeline entries.
/// </summary>
internal sealed class TimelineMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await TimelineIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
