using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query Contact Center work state by activity, agent, or reservation.
/// </summary>
public sealed class ContactCenterWorkStateIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity identifier the work state belongs to.
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
    /// Gets or sets the user identifier of the agent the activity is assigned to.
    /// </summary>
    public string AssignedToId { get; set; }
}
