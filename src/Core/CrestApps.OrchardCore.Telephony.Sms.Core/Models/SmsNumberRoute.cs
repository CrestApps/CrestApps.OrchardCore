using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Models;

/// <summary>
/// The one genuinely-new routing concept of the SMS portal: it binds a dialed number (DID) to a target for
/// inbound SMS. This is the SMS analog of a Contact Center entry point, but SMS-shaped and not Voice-gated.
/// Every number scenario falls out of this one table — a personal number, one agent with several numbers, and
/// a department (queue) with several numbers.
/// </summary>
/// <remarks>
/// The provider is <b>not</b> stored here — it is a property of the number
/// (<c>OmnichannelChannelEndpoint.ProviderName</c>), read from the bound endpoint at send time.
/// </remarks>
public sealed class SmsNumberRoute : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets a human-friendly name for the route.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets an optional description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the bound <c>OmnichannelChannelEndpoint</c> (the DID).
    /// </summary>
    public string EndpointId { get; set; }

    /// <summary>
    /// Gets or sets the dialed number (DID) served by this route, denormalized from the bound endpoint so the
    /// inbound pipeline can resolve the route by the received <c>ServiceAddress</c> without an extra lookup.
    /// </summary>
    public string DialedNumber { get; set; }

    /// <summary>
    /// Gets or sets what the route sends inbound messages to: a single agent (default) or a queue.
    /// </summary>
    public SmsNumberRouteTargetType TargetType { get; set; } = SmsNumberRouteTargetType.Agent;

    /// <summary>
    /// Gets or sets the target identifier: an agent profile id for <see cref="SmsNumberRouteTargetType.Agent"/>,
    /// or an <c>ActivityQueue</c> id for <see cref="SmsNumberRouteTargetType.Queue"/>.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets how inbound messages for a queue target are distributed. Ignored for an agent target.
    /// </summary>
    public SmsNumberRouteDistributionMode DistributionMode { get; set; } = SmsNumberRouteDistributionMode.SharedPool;

    /// <summary>
    /// Gets or sets an optional auto-reply sent to the customer on the first inbound message.
    /// </summary>
    public string AutoReplyMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the route is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the route was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the route was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
