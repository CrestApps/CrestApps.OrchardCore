using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Where a messaging number's inbound messages went before inbound entry points answered messaging channels, stored in
/// the address's properties. Only the upgrade reads it now: it becomes an entry point for the channel, with
/// <see cref="MessagingEntryPointSettings"/>, and is removed from the address.
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
