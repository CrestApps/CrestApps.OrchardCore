using CrestApps.OrchardCore.Telephony.Sms.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Telephony.Sms.BackgroundTasks;

/// <summary>
/// Periodically fans out queued SMS broadcasts. Each recipient becomes an individual 1:1 thread; progress is
/// persisted after every recipient so a restart resumes without re-sending. The sweep is a no-op when there
/// are no queued or in-progress broadcasts.
/// </summary>
[BackgroundTask(
    Title = "SMS Broadcast Fan-out",
    Schedule = "* * * * *",
    Description = "Sends queued SMS broadcasts to their recipients as individual 1:1 threads, resuming safely after a restart.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class SmsBroadcastBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var broadcastService = serviceProvider.GetRequiredService<ISmsBroadcastService>();
        var logger = serviceProvider.GetRequiredService<ILogger<SmsBroadcastBackgroundTask>>();

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
            logger.LogError(ex, "An error occurred while processing SMS broadcasts.");
        }
    }
}
