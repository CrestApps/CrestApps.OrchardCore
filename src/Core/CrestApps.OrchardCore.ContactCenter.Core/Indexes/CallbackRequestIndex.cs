using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query callback requests.
/// </summary>
public sealed class CallbackRequestIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the callback status.
    /// </summary>
    public CallbackRequestStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the callback becomes due.
    /// </summary>
    public DateTime ScheduledUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the current promotion lease expires, when the callback is claimed.
    /// </summary>
    public DateTime? LeaseExpiresUtc { get; set; }
}
