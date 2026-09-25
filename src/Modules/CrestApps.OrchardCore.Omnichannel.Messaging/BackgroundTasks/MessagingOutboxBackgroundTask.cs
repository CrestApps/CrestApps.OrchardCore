using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.BackgroundTasks;

/// <summary>
/// Re-attempts outbound messages the provider refused. The send an agent makes is still tried inside their request so
/// the composer answers immediately, but a provider that is briefly unreachable no longer costs them the
/// message: it stays queued and this pass retries it on a widening schedule until it is accepted or the schedule
/// is exhausted.
/// </summary>
[BackgroundTask(
    Title = "Messaging Outbound Outbox",
    Schedule = "* * * * *",
    Description = "Retries outbound messages, on every channel, that a provider refused, on a widening backoff schedule.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class MessagingOutboxBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var outbox = serviceProvider.GetRequiredService<IMessagingOutbox>();
        var logger = serviceProvider.GetRequiredService<ILogger<MessagingOutboxBackgroundTask>>();

        try
        {
            await outbox.DispatchDueAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while retrying queued outbound messages.");
        }
    }
}
