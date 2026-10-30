using CrestApps.OrchardCore.Calendars.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Calendars.Migrations;

internal sealed class CalendarMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await CalendarIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    // The categories events are put in.
    public async Task<int> UpdateFrom1Async()
    {
        await CalendarIndexSchema.CreateCategoriesAsync(SchemaBuilder);

        return 2;
    }
}
