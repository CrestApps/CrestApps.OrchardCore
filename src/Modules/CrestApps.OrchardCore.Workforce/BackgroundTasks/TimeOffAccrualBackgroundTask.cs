using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.BackgroundTasks;

/// <summary>
/// Accrues every worker's time-off policies for the periods that have ended. A run writes each period once, so it is
/// safe to run often.
/// </summary>
[BackgroundTask(
    Title = "Time off accrual",
    Schedule = "15 * * * *",
    Description = "Adds what time-off policies accrue to workers' balances for the periods that have ended, and expires what is above the carryover limit at each reset date.",
    LockTimeout = 1_000,
    LockExpiration = 3_600_000)]
public sealed class TimeOffAccrualBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var now = await serviceProvider.GetRequiredService<ILocalClock>().GetLocalNowAsync();

        await serviceProvider.GetRequiredService<ITimeOffAccrualService>().RunAsync(DateOnly.FromDateTime(now.DateTime), cancellationToken);
    }
}
