using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query activity reservations.
/// </summary>
public sealed class ActivityReservationIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity identifier that is reserved.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the agent the activity is reserved for.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status of the reservation.
    /// </summary>
    public ReservationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the reservation expires.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }
}
