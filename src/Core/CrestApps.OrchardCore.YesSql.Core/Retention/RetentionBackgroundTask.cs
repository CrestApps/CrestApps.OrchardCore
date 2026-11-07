using CrestApps.OrchardCore.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Retention;

/// <summary>
/// The one retention task: once a day it runs every <see cref="IRetentionPolicy"/> the tenant's features registered.
/// </summary>
[BackgroundTask(
    Title = "Data retention",
    Schedule = "30 3 * * *",
    Description = "Removes logs, deliveries, codes and other records older than their retention windows.",
    LockTimeout = 10_000,
    LockExpiration = 300_000)]
public sealed class RetentionBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<RetentionBackgroundTask>>();
        var options = serviceProvider.GetRequiredService<IOptions<RetentionOptions>>().Value;

        try
        {
            var report = await RetentionRunner.RunAsync(
                serviceProvider.GetServices<IRetentionPolicy>(),
                serviceProvider.GetRequiredService<ISession>(),
                serviceProvider.GetRequiredService<IClock>().UtcNow,
                options.PurgeBatchSize > 0 ? options.PurgeBatchSize : RetentionOptions.DefaultPurgeBatchSize,
                options.MaxPurgeBatchesPerCycle > 0 ? options.MaxPurgeBatchesPerCycle : RetentionOptions.DefaultMaxPurgeBatchesPerCycle,
                logger,
                cancellationToken);

            if (report.WorkRemains)
            {
                logger.LogWarning(
                    "Retention removed {PurgedCount} records but did not reach steady state for: {Policies}. Raise 'CrestApps:Retention:MaxPurgeBatchesPerCycle' or shorten the retention windows.",
                    report.TotalPurged,
                    string.Join(", ", report.Policies.Where(policy => policy.WorkRemains).Select(policy => policy.Name)));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while removing expired records.");
        }
    }
}
