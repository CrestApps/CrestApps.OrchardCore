using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Routes inbound and outbound voice calls for the Contact Center while keeping provider-specific media
/// execution in Telephony or PBX provider modules.
/// </summary>
public interface IVoiceContactCenterCallRouter
{
    /// <summary>
    /// Determines whether an outbound voice call can be routed through the configured provider.
    /// </summary>
    /// <param name="providerName">The optional provider technical name.</param>
    /// <returns><see langword="true"/> when an outbound voice provider is available; otherwise <see langword="false"/>.</returns>
    bool CanRouteOutbound(string providerName = null);

    /// <summary>
    /// Resolves the outbound provider technical name that would be used for a route.
    /// </summary>
    /// <param name="providerName">The optional provider technical name.</param>
    /// <returns>The resolved provider technical name, or <see langword="null"/> when no provider is available.</returns>
    string GetOutboundProviderName(string providerName = null);

    /// <summary>
    /// Routes a normalized inbound voice event into Contact Center work.
    /// </summary>
    /// <param name="inboundEvent">The normalized inbound voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The inbound routing result.</returns>
    Task<InboundVoiceRoutingResult> RouteInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Routes an outbound voice dial request to a Contact Center voice provider.
    /// </summary>
    /// <param name="request">The outbound voice dial request.</param>
    /// <param name="providerName">The optional provider technical name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The voice provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> RouteOutboundAsync(ContactCenterDialRequest request, string providerName = null, CancellationToken cancellationToken = default);
}
