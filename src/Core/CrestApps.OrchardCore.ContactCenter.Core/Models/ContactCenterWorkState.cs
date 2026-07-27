using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the routing-owned work state for a single CRM activity. Contact Center reservation, offer,
/// assignment and dialer transitions write this aggregate instead of the CRM activity, so a concurrent CRM
/// edit and a routing transition can never invalidate one another.
/// </summary>
public sealed class ContactCenterWorkState : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the CRM activity this work state belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the routing-owned assignment status of the activity.
    /// </summary>
    public ActivityAssignmentStatus AssignmentStatus { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the reservation currently holding the activity.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent the activity is reserved for.
    /// </summary>
    public string ReservedById { get; set; }

    /// <summary>
    /// Gets or sets the user name of the agent the activity is reserved for.
    /// </summary>
    public string ReservedByUsername { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the activity was reserved.
    /// </summary>
    public DateTime? ReservedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the current reservation expires when it is not accepted.
    /// </summary>
    public DateTime? ReservationExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent the activity is assigned to.
    /// </summary>
    public string AssignedToId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the agent the activity is assigned to.
    /// </summary>
    public string AssignedToUsername { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the activity was assigned.
    /// </summary>
    public DateTime? AssignedToUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of outbound attempts the dialer has made for the activity.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the work state was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the work state was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
