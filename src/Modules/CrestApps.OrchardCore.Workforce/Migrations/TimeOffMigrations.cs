using CrestApps.OrchardCore.Workforce.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class TimeOffMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await TimeOffIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
