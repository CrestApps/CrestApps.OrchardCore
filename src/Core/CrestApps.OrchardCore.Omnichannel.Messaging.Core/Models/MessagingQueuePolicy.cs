namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// The parts of a queue the messaging workspace needs: how long a contact may wait for a first reply, and which calendar
/// says when the queue is open. Asking for the policy rather than the queue entity is what lets the workspace run
/// on a tenant that has no Work Distribution feature at all.
/// </summary>
/// <param name="Exists">Whether a queue with that identifier exists.</param>
/// <param name="FirstResponseTargetSeconds">The first-response target in seconds; zero means the queue has not set one.</param>
/// <param name="BusinessHoursCalendarId">The business-hours calendar, or null when the queue keeps none.</param>
public readonly record struct MessagingQueuePolicy(bool Exists, int FirstResponseTargetSeconds, string BusinessHoursCalendarId)
{
    /// <summary>
    /// The policy reported for a queue that cannot be found, including on a tenant with no queues at all.
    /// </summary>
    public static MessagingQueuePolicy NotFound { get; } = new(false, 0, null);
}
