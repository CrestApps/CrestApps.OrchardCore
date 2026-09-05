using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Sms.Workspace.BackgroundTasks;

/// <summary>
/// Periodically returns routed (push-assigned) SMS conversations that the assigned agent has not picked up
/// within the grace window to their queue's shared pool, so a message never stalls in one inbox. The sweep is a
/// no-op when there are no unpicked routed conversations.
/// </summary>
[BackgroundTask(
    Title = "SMS Routed Reassignment",
    Schedule = "* * * * *",
    Description = "Returns unpicked routed SMS conversations to their queue pool so another agent can take them.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class SmsRoutedReassignmentBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var reassignmentService = serviceProvider.GetRequiredService<ISmsRoutedReassignmentService>();
        var logger = serviceProvider.GetRequiredService<ILogger<SmsRoutedReassignmentBackgroundTask>>();

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
            logger.LogError(ex, "An error occurred while reassigning stale routed SMS conversations.");
        }
    }
}
