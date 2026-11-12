using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Calendars.Migrations;

internal sealed class CalendarMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await CalendarIndexSchema.CreateAsync(SchemaBuilder);
        await CreateReportIndexAsync();

        return 4;
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

    // Feed secrets are stored hashed (and protected, to show the address again): the calendars that have a secret from
    // before get its hash and protected copy, and are indexed again, so the index holds the hash. The plain secret stays
    // one release, for a rollback.
    public int UpdateFrom3Async()
    {
        ShellScope.AddDeferredTask(scope => ActivatorUtilities.CreateInstance<CalendarFeedTokenBackfill>(scope.ServiceProvider).RunAsync());

        return 4;
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
