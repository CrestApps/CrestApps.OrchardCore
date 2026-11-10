using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.BackgroundTasks;

/// <summary>
/// Raises the due "starting soon" and "completed" reminders every minute, and forgets reminders older than a week.
/// </summary>
[BackgroundTask(
    Title = "Calendar reminders",
    Schedule = "* * * * *",
    Description = "Raises the 'starting soon' and 'completed' moments of calendar events.",
    LockTimeout = 1_000,
    LockExpiration = 120_000)]
public sealed class CalendarReminderBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            // Old reminder records are removed by the platform retention task (CalendarReminderLogRetentionPolicy).
            run.AddItems(await serviceProvider.GetRequiredService<CalendarReminderService>().RunAsync(cancellationToken), "raised");
        });
    }
}
