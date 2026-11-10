using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Workforce.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.BackgroundTasks;

/// <summary>
/// Brings the days of approved time off into Time Tracker as they start; entries aren't kept ahead of their time.
/// </summary>
[BackgroundTask(
    Title = "Time off to Time Tracker",
    Schedule = "5 * * * *",
    Description = "Keeps the days of approved time off that started yesterday or today in Time Tracker.",
    LockTimeout = 1_000,
    LockExpiration = 1_800_000)]
public sealed class TimeOffTimeTrackerBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            var now = await serviceProvider.GetRequiredService<ILocalClock>().GetLocalNowAsync();

            run.AddItems(await serviceProvider.GetRequiredService<TimeOffTimeTrackerRefresher>().RunAsync(DateOnly.FromDateTime(now.DateTime), cancellationToken), "refreshed");
        });
    }
}
