using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Defines optional Contact Center voice operations a PBX or telephony provider can implement in addition to soft-phone call control.
/// </summary>
public interface IContactCenterVoiceProvider
{
    /// <summary>
    /// Gets the stable technical name used to resolve the provider.
    /// </summary>
    string TechnicalName { get; }

    /// <summary>
    /// Gets the localized, human-readable name of the provider.
    /// </summary>
    LocalizedString Name { get; }

    /// <summary>
    /// Gets the provider capabilities supported for Contact Center orchestration.
    /// </summary>
    ContactCenterVoiceProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Places an outbound dialer call for a reserved activity and agent.
    /// </summary>
    /// <param name="request">The dial request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> DialAsync(ContactCenterDialRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns an existing provider call to an agent for a Contact Center activity.
    /// </summary>
    /// <param name="request">The call assignment request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> AssignCallAsync(ContactCenterCallAssignmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places or moves a provider call into a provider-side queue when the provider supports queue ownership.
    /// </summary>
    /// <param name="request">The queue call request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> QueueCallAsync(ContactCenterQueueCallRequest request, CancellationToken cancellationToken = default);
}
