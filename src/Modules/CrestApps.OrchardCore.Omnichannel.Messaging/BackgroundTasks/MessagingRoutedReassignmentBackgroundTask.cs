using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.BackgroundTasks;

/// <summary>
/// Periodically returns routed (push-assigned) conversations that the assigned agent has not picked up
/// within the grace window to their queue's shared pool, so a message never stalls in one inbox. The sweep is a
/// no-op when there are no unpicked routed conversations.
/// </summary>
[BackgroundTask(
    Title = "Messaging Routed Reassignment",
    Schedule = "* * * * *",
    Description = "Returns unpicked routed conversations to their queue pool so another agent can take them.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class MessagingRoutedReassignmentBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var reassignmentService = serviceProvider.GetRequiredService<IMessagingRoutedReassignmentService>();
        var logger = serviceProvider.GetRequiredService<ILogger<MessagingRoutedReassignmentBackgroundTask>>();

        try
        {
            await reassignmentService.ReassignStaleAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while reassigning stale routed conversations.");
        }
    }
}
