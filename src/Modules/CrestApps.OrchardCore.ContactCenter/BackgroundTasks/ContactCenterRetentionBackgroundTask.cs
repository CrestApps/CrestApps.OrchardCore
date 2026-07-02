using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Purges durable interaction events beyond the configured data-governance retention window once a day.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Data Retention",
    Schedule = "0 3 * * *",
    Description = "Purges durable interaction events older than the configured retention window.",
    LockTimeout = 10_000,
    LockExpiration = 300_000)]
public sealed class ContactCenterRetentionBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ContactCenterRetentionOptions>>().Value;

        if (options.InteractionEventRetentionDays <= 0)
        {
            return;
        }

        var clock = serviceProvider.GetRequiredService<IClock>();
        var retentionService = serviceProvider.GetRequiredService<IContactCenterRetentionService>();
        var logger = serviceProvider.GetRequiredService<ILogger<ContactCenterRetentionBackgroundTask>>();

        var cutoff = clock.UtcNow.AddDays(-options.InteractionEventRetentionDays);

        try
        {
            await retentionService.PurgeInteractionEventsAsync(cutoff, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while purging expired Contact Center interaction events.");
        }
    }
}
