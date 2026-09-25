using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// The inbound-routing configuration attached to an <c>OmnichannelChannelEndpoint</c> of a messaging channel. Stored in
/// the endpoint's extensible properties by the messaging workspace feature, so a single channel-endpoint screen manages
/// the endpoint, its provider, and where its inbound messages route — no separate routing catalog.
/// </summary>
public sealed class MessagingEndpointRoutingSettings
{
    /// <summary>
    /// Gets or sets what inbound messages on this number route to: a single agent or a queue (department).
    /// </summary>
    public ConversationRouteTargetType TargetType { get; set; } = ConversationRouteTargetType.Agent;

    /// <summary>
    /// Gets or sets the target identifier: an agent profile id for <see cref="ConversationRouteTargetType.Agent"/>,
    /// or an <c>ActivityQueue</c> id for <see cref="ConversationRouteTargetType.Queue"/>. Empty means "no routing"
    /// (inbound lands in the unassigned inbox).
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets how inbound messages for a queue target are distributed. Ignored for an agent target.
    /// </summary>
    public ConversationDistributionMode DistributionMode { get; set; } = ConversationDistributionMode.SharedPool;

    /// <summary>
    /// Gets or sets an optional auto-reply sent to the contact on their first inbound message.
    /// </summary>
    public string AutoReplyMessage { get; set; }
}
