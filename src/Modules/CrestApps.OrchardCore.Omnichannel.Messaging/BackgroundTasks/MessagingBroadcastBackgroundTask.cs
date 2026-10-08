using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.BackgroundTasks;

/// <summary>
/// Periodically fans out queued broadcasts. Each recipient becomes an individual 1:1 thread; progress is
/// persisted after every recipient so a restart resumes without re-sending. The sweep is a no-op when there
/// are no queued or in-progress broadcasts.
/// </summary>
[BackgroundTask(
    Title = "Messaging Broadcast Fan-out",
    Schedule = "* * * * *",
    Description = "Sends queued broadcasts to their recipients as individual 1:1 threads, resuming safely after a restart.",
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
