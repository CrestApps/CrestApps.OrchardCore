using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a short-lived lock that reserves an activity for an agent before assignment is finalized.
/// </summary>
public sealed class ActivityReservation : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the CRM activity that is reserved.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the queue the reservation originated from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the queue item that is reserved.
    /// </summary>
    public string QueueItemId { get; set; }

    /// <summary>
    /// Gets or sets the agent the activity is reserved for.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status of the reservation.
    /// </summary>
    public ReservationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the reservation was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the reservation expires when not accepted.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the reservation was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
