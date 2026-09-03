namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// Returns routed (push-assigned) SMS conversations that the assigned agent has not picked up within the grace
/// window back to their queue's shared pool, so another member can take them instead of a message stalling in
/// one agent's inbox. Invoked periodically by a background task.
/// </summary>
public interface ISmsRoutedReassignmentService
{
    /// <summary>
    /// Re-pools every routed conversation whose pickup grace window has elapsed.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of conversations returned to their pool.</returns>
    Task<int> ReassignStaleAsync(CancellationToken cancellationToken = default);
}
