using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Executes provider-owned call assignment and queue placement.
/// </summary>
public interface IContactCenterVoiceQueueAssignmentProvider
{
    /// <summary>
    /// Assigns an existing provider call to an agent.
    /// </summary>
    /// <param name="request">The assignment request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> AssignCallAsync(
        ContactCenterCallAssignmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Places or moves a call into a provider-owned queue.
    /// </summary>
    /// <param name="request">The queue request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> QueueCallAsync(
        ContactCenterQueueCallRequest request,
        CancellationToken cancellationToken = default);
}
