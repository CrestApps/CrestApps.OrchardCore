using System.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.BackgroundTasks;

/// <summary>
/// Deletes delivery log entries older than any window sending limits and health are counted over, so the log stays the
/// size of a month's mail. The suppression list is never pruned.
/// </summary>
/// <remarks>
/// A busy address logs tens of thousands of entries a day, so the task runs every ten minutes and deletes batch after
/// batch until nothing is left to delete or its time is up, rather than a fixed number an hour that falls behind.
/// </remarks>
[BackgroundTask(
    Title = "Email Delivery Log Pruning",
    Schedule = "*/10 * * * *",
    Description = "Deletes email delivery log entries older than 30 days.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class EmailDeliveryLogPruningBackgroundTask : IBackgroundTask
{
    private const int BatchSize = 500;

    // A run stops after this long even with entries left, well inside the ten minutes its lock lasts and before the next
    // run; that run carries on where this one stopped.
    private static readonly TimeSpan _timeBudget = TimeSpan.FromMinutes(4);

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var log = serviceProvider.GetRequiredService<IEmailDeliveryLog>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var cutoff = clock.UtcNow.Subtract(EmailHealthPolicy.DeliveryLogRetention);
        var started = Stopwatch.GetTimestamp();
        var deleted = 0;

        while (!cancellationToken.IsCancellationRequested && Stopwatch.GetElapsedTime(started) < _timeBudget)
        {
            var count = await log.PruneAsync(cutoff, BatchSize, cancellationToken);

            if (count == 0)
            {
                break;
            }

            // Each batch is committed on its own, so a long run holds no lock on the log for longer than one batch.
            await session.SaveChangesAsync(cancellationToken);
            deleted += count;

            if (count < BatchSize)
            {
                break;
            }
        }

        if (deleted > 0)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<EmailDeliveryLogPruningBackgroundTask>>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Deleted {Count} email delivery log entries older than {Days} days.", deleted, (int)EmailHealthPolicy.DeliveryLogRetention.TotalDays);
            }
        }
    }
}
