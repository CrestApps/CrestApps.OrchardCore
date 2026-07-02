using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Expires stale agent reservations and assigns waiting work to available agents across enabled queues.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Reservation and Assignment",
    Schedule = "* * * * *",
    Description = "Expires stale reservations and assigns queued activities to available agents.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class ReservationExpiryBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var reservationService = serviceProvider.GetRequiredService<IActivityReservationService>();
        var assignmentService = serviceProvider.GetRequiredService<IActivityAssignmentService>();
        var queueService = serviceProvider.GetRequiredService<IActivityQueueService>();
        var queueManager = serviceProvider.GetRequiredService<IActivityQueueManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<ReservationExpiryBackgroundTask>>();

        await reservationService.ExpireDueAsync(cancellationToken);

        var queues = await queueManager.ListEnabledAsync(cancellationToken);

        foreach (var queue in queues)
        {
            try
            {
                await queueService.OverflowDueAsync(queue, cancellationToken);
                await assignmentService.AssignQueueAsync(queue.ItemId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while assigning work for queue '{QueueId}'.", queue.ItemId);
            }
        }
    }
}
