using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.TimeTracker.Migrations;

internal sealed class TimeTrackerMigrations : DataMigration
{
    private readonly ResumableSteps _steps;

    public TimeTrackerMigrations(ResumableSteps steps)
    {
        _steps = steps;
    }

    public async Task<int> CreateAsync()
    {
        await TimeTrackerIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    /// <summary>
    /// Entries remember what their time counts against (such as a ticket's agreement); the entries logged before are
    /// charged by a resumable step, so their time stays where it counts today.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await TimeTrackerIndexSchema.AddChargedToAsync(SchemaBuilder);

        await _steps.RequestAsync(TimeEntryChargeStep.StepName);

        return 2;
    }
}
