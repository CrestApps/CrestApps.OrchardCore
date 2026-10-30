using CrestApps.OrchardCore.Calendars.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Calendars.Migrations;

internal sealed class CalendarMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await CalendarIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
