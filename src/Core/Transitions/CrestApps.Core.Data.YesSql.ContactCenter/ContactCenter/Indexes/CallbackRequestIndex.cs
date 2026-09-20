using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.ContactCenter.Models;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Indexes;

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

    /// <summary>
    /// Gets or sets the UTC time the callback was last modified, which is the settlement time for a callback
    /// that has reached a terminal status and is what retention ages it by.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
