using CrestApps.OrchardCore.Workforce.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Workforce.BackgroundTasks;

/// <summary>
/// Works out again the timesheets of the workers who punched or had a shift lately, so missed punches and no-shows
/// are raised without anyone opening their timesheet.
/// </summary>
[BackgroundTask(
    Title = "Timesheet refresh",
    Schedule = "40 * * * *",
    Description = "Works out the timesheets of workers who punched or had a shift in the last day and a half, raising missed punches and no-shows.",
    LockTimeout = 1_000,
    LockExpiration = 3_600_000)]
public sealed class TimesheetRefreshBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return serviceProvider.GetRequiredService<TimesheetRefresher>().RefreshAsync(cancellationToken);
    }
}
