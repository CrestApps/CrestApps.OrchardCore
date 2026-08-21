using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;

/// <summary>
/// The YesSql index used to query <c>SmsBroadcast</c> documents by name and status.
/// </summary>
public sealed class SmsBroadcastIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the broadcast name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the broadcast status, stored as its string name.
    /// </summary>
    public string Status { get; set; }
}
