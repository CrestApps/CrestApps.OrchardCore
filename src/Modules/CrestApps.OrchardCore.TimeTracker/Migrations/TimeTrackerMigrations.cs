using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.TimeTracker.Migrations;

internal sealed class TimeTrackerMigrations : DataMigration
{
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

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<ResumableSteps>().RequestAsync(TimeEntryChargeStep.StepName));

        return 2;
    }
}
