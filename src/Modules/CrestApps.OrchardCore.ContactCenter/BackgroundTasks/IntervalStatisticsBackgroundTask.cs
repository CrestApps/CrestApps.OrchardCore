using CrestApps.OrchardCore.ContactCenter.IntervalStatistics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Keeps the 15-minute interval statistics of queues and agents up to date, a few days per run. The background task
/// lock is what makes the roller single-writer across nodes.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Interval Statistics",
    Schedule = "*/5 * * * *",
    Description = "Computes the 15-minute statistics of every queue and agent for today, backfills earlier days, and runs a rebuild an administrator asked for.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]
public sealed class IntervalStatisticsBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var roller = serviceProvider.GetRequiredService<IntervalStatisticsRoller>();
        var logger = serviceProvider.GetRequiredService<ILogger<IntervalStatisticsBackgroundTask>>();

        try
        {
            await roller.RollAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            // A rebuild was asked for during the run. The next run starts from it.
            logger.LogInformation("The Contact Center interval statistics changed during a run; the next run continues.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "An error occurred while updating the Contact Center interval statistics.");
        }
    }
}
