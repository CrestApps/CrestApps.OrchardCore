using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Retries provider webhook inbox messages whose immediate persisted dispatch did not complete. Each due
/// message is isolated in its own fresh child scope so a poison delivery never blocks the rest of the batch.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Provider Webhook Inbox Dispatch",
    Schedule = "* * * * *",
    Description = "Processes durable provider webhook deliveries with bounded retries and dead-lettering.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class ProviderWebhookInboxBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Voice);

        if (workLease is null)
        {
            return;
        }

        var inbox = serviceProvider.GetRequiredService<IProviderWebhookInbox>();
        var logger = serviceProvider.GetRequiredService<ILogger<ProviderWebhookInboxBackgroundTask>>();

        try
        {
            var processed = await inbox.DispatchDueAsync(cancellationToken);

            if (processed > 0 && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Processed {Count} provider webhook inbox message(s).", processed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                OperationalLogRedactor.RedactException(exception),
                "An error occurred while dispatching the provider webhook inbox.");
        }
    }
}
