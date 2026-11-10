using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Workforce.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Workforce.BackgroundTasks;

/// <summary>
/// Brings the punches made before the time clock kept time in Time Tracker in, a batch of workers at a time, until
/// every worker with a login is done.
/// </summary>
[BackgroundTask(
    Title = "Time clock to Time Tracker",
    Schedule = "*/5 * * * *",
    Description = "Brings earlier punches into Time Tracker, 25 workers at a time, once.",
    LockTimeout = 1_000,
    LockExpiration = 1_800_000)]
public sealed class TimeClockTrackerBackfillBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<TimeClockTrackerBackfill>().RunAsync(25, cancellationToken), "synced"));
    }
}
