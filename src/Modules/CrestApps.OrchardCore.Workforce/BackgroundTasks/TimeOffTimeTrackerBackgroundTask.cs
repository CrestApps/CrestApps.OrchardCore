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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var now = await serviceProvider.GetRequiredService<ILocalClock>().GetLocalNowAsync();

        await serviceProvider.GetRequiredService<TimeOffTimeTrackerRefresher>().RunAsync(DateOnly.FromDateTime(now.DateTime), cancellationToken);
    }
}
