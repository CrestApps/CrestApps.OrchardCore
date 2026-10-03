using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Finds where messages to one of the business's addresses go. The routing lives on the inbound entry point that
/// answers the address on the conversation's channel, the one place a number's inbound traffic is routed from.
/// </summary>
public interface IMessagingInboundRoutingResolver
{
    /// <summary>
    /// Resolves the routing of an address on a channel.
    /// </summary>
    /// <param name="address">The address the message was sent to.</param>
    /// <param name="channel">The messaging channel, such as SMS.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The routing, or <see langword="null"/> when no enabled entry point answers the address on the channel.</returns>
    Task<MessagingInboundRouting> ResolveAsync(OmnichannelChannelEndpoint address, string channel, CancellationToken cancellationToken = default);
}
