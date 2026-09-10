using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Executes Contact Center call-control operations supported by a voice provider.
/// </summary>
public interface IContactCenterVoiceCallControlProvider
{
    /// <summary>
    /// Places an outbound dialer call.
    /// </summary>
    /// <param name="request">The dial request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> DialAsync(
        ContactCenterDialRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects a live provider call to the selected agent.
    /// </summary>
    /// <param name="request">The connect request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> ConnectToAgentAsync(
        ContactCenterConnectRequest request,
        CancellationToken cancellationToken = default);
}
