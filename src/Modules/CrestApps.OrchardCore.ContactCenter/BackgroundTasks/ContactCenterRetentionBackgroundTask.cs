using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Drains every Contact Center table of records beyond its configured data-governance retention window once a
/// day.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Data Retention",
    Schedule = "0 3 * * *",
    Description = "Purges Contact Center records older than their configured retention windows.",
    LockTimeout = 10_000,
    LockExpiration = 300_000)]
public sealed class ContactCenterRetentionBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var retentionService = serviceProvider.GetRequiredService<IContactCenterRetentionService>();
        var logger = serviceProvider.GetRequiredService<ILogger<ContactCenterRetentionBackgroundTask>>();

        try
        {
            var report = await retentionService.PurgeAsync(cancellationToken);

            if (report.WorkRemains)
            {
                // Without this the cycle would look successful while the database kept growing, which is the
                // failure mode a silently truncating batch cap produces.
                var starvedEntities = string.Join(
                    ", ",
                    report.Entities.Where(entity => entity.WorkRemains).Select(entity => entity.EntityName));

                logger.LogWarning(
                    "Contact Center retention purged {PurgedCount} records but did not reach steady state for: {StarvedEntities}. Raise 'CrestApps_ContactCenter:Retention:MaxPurgeBatchesPerCycle' or shorten the retention windows.",
                    report.TotalPurged,
                    starvedEntities);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while purging expired Contact Center records.");
        }
    }
}
