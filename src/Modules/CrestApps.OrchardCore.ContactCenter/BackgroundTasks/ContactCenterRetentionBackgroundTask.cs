using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
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
        var clock = serviceProvider.GetRequiredService<IClock>();

        if (!RetentionCutoffCalculator.TryComputeCutoff(clock.UtcNow, options, out var cutoff))
        {
            return;
        }

        var retentionService = serviceProvider.GetRequiredService<IContactCenterRetentionService>();
        var logger = serviceProvider.GetRequiredService<ILogger<ContactCenterRetentionBackgroundTask>>();

        try
        {
            await retentionService.PurgeInteractionEventsAsync(cutoff, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while purging expired Contact Center interaction events.");
        }
    }
}
