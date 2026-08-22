using CrestApps.OrchardCore.Sms.Workspace.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Models;

/// <summary>
/// The SMS inbound-routing configuration attached to an <c>OmnichannelChannelEndpoint</c> (a DID). Stored in
/// the endpoint's extensible properties by the SMS portal feature, so a single channel-endpoint screen manages
/// the number, its provider, and where its inbound SMS routes — no separate routing catalog.
/// </summary>
public sealed class SmsEndpointRoutingSettings
{
    /// <summary>
    /// Gets or sets what inbound messages on this number route to: a single agent or a queue (department).
    /// </summary>
    public SmsNumberRouteTargetType TargetType { get; set; } = SmsNumberRouteTargetType.Agent;

    /// <summary>
    /// Gets or sets the target identifier: an agent profile id for <see cref="SmsNumberRouteTargetType.Agent"/>,
    /// or an <c>ActivityQueue</c> id for <see cref="SmsNumberRouteTargetType.Queue"/>. Empty means "no routing"
    /// (inbound lands in the unassigned inbox).
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets how inbound messages for a queue target are distributed. Ignored for an agent target.
    /// </summary>
    public SmsNumberRouteDistributionMode DistributionMode { get; set; } = SmsNumberRouteDistributionMode.SharedPool;

    /// <summary>
    /// Gets or sets an optional auto-reply sent to the customer on their first inbound message.
    /// </summary>
    public string AutoReplyMessage { get; set; }
}
