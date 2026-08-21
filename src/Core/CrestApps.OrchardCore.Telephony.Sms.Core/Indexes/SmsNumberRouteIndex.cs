using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;

/// <summary>
/// The YesSql index used to query <c>SmsNumberRoute</c> documents: resolve the route for an inbound DID and
/// list the routes bound to a given endpoint or target.
/// </summary>
public sealed class SmsNumberRouteIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the bound endpoint (DID).
    /// </summary>
    public string EndpointId { get; set; }

    /// <summary>
    /// Gets or sets the dialed number served by this route (the routing key).
    /// </summary>
    public string DialedNumber { get; set; }

    /// <summary>
    /// Gets or sets the target type (Agent or Queue), stored as its string name.
    /// </summary>
    public string TargetType { get; set; }

    /// <summary>
    /// Gets or sets the target identifier (agent profile id or queue id).
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the route is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}
