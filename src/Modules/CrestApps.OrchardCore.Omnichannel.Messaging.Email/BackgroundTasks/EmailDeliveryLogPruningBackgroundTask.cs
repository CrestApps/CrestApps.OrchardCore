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
[BackgroundTask(
    Title = "Email Delivery Log Pruning",
    Schedule = "17 * * * *",
    Description = "Deletes email delivery log entries older than 30 days.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class EmailDeliveryLogPruningBackgroundTask : IBackgroundTask
{
    private const int MaxEntriesPerRun = 2_000;

    private const int BatchSize = 500;

    private static readonly TimeSpan _retention = TimeSpan.FromDays(30);

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var log = serviceProvider.GetRequiredService<IEmailDeliveryLog>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var cutoff = clock.UtcNow.Subtract(_retention);
        var deleted = 0;

        while (deleted < MaxEntriesPerRun && !cancellationToken.IsCancellationRequested)
        {
            var count = await log.PruneAsync(cutoff, BatchSize, cancellationToken);

            if (count == 0)
            {
                break;
            }

            await session.SaveChangesAsync(cancellationToken);
            deleted += count;
        }

        if (deleted > 0)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<EmailDeliveryLogPruningBackgroundTask>>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Deleted {Count} email delivery log entries older than {Days} days.", deleted, (int)_retention.TotalDays);
            }
        }
    }
}
