using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Scheduling.Migrations;

internal sealed class SchedulingMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchedulingIndexSchema.CreateAsync(SchemaBuilder);
        await CreateReportIndexesAsync();

        return 2;
    }

    // The indexes reports read appointments and waitlist entries through, newest first by their main date, a page at a time.
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexesAsync();

        return 2;
    }

    private async Task CreateReportIndexesAsync()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<AppointmentIndex>(
            SchemaBuilder,
            "IDX_AppointmentIndex_Report",
            SchedulingConstants.CollectionName,
            "StartUtc", "Id");

        await IndexTableIndexCreation.CreateIfMissingAsync<WaitlistEntryIndex>(
            SchemaBuilder,
            "IDX_WaitlistEntryIndex_Report",
            SchedulingConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
