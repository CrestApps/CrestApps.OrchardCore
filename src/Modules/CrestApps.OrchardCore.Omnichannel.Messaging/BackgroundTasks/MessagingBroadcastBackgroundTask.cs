using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.BackgroundTasks;

/// <summary>
/// Periodically hands queued broadcasts to the outreach engine, which sends each recipient an individual 1:1 thread and
/// keeps its progress, so a restart resumes without re-sending. The sweep is a no-op when no broadcast is waiting.
/// </summary>
[BackgroundTask(
    Title = "Messaging Broadcast Fan-out",
    Schedule = "* * * * *",
    Description = "Starts sending queued broadcasts through the outreach engine, each recipient in an individual 1:1 thread.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class MessagingBroadcastBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var broadcastService = serviceProvider.GetRequiredService<IMessagingBroadcastService>();
        var logger = serviceProvider.GetRequiredService<ILogger<MessagingBroadcastBackgroundTask>>();

        try
        {
            await broadcastService.ProcessPendingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing broadcasts.");
        }
    }
}
