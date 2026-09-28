using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;

/// <summary>
/// The YesSql index used to query <c>MessagingBroadcast</c> documents by name and status.
/// </summary>
public sealed class MessagingBroadcastIndex : CatalogItemIndex, INameAwareIndex
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
