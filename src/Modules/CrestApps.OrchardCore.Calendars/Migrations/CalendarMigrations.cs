using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Calendars.Migrations;

internal sealed class CalendarMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await CalendarIndexSchema.CreateAsync(SchemaBuilder);
        await CreateReportIndexAsync();

        return 3;
    }

    // The categories events are put in.
    public async Task<int> UpdateFrom1Async()
    {
        await CalendarIndexSchema.CreateCategoriesAsync(SchemaBuilder);

        return 2;
    }

    // The index reports read events through, newest first by when they start, a page at a time.
    public async Task<int> UpdateFrom2Async()
    {
        await CreateReportIndexAsync();

        return 3;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<CalendarEventIndex>(
            SchemaBuilder,
            "IDX_CalendarEventIndex_Report",
            CalendarsConstants.CollectionName,
            "StartUtc", "Id");
    }
}
