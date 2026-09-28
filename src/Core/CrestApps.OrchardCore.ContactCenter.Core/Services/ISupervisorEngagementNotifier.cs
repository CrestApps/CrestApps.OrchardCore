using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Pushes supervisor engagements to the supervisor's own clients, and a supervisor's messages to an agent, over the
/// real-time channel. With real-time disabled there is no implementation and nothing is pushed.
/// </summary>
public interface ISupervisorEngagementNotifier
{
    /// <summary>
    /// Tells the supervisor's clients about their engagement.
    /// </summary>
    /// <param name="notification">The engagement notification.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NotifyEngagementAsync(SupervisorEngagementNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers a supervisor's message to the agent's clients.
    /// </summary>
    /// <param name="notification">The message.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NotifyAgentMessageAsync(SupervisorMessageNotification notification, CancellationToken cancellationToken = default);
}
