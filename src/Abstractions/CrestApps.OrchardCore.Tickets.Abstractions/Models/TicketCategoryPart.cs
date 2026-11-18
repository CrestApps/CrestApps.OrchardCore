using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// What a ticket category gives the tickets filed under it, on a taxonomy term (the <c>TicketCategory</c> term type). Every
/// value is optional: a sub-category takes what it leaves empty from the nearest category above it that sets it.
/// </summary>
public sealed class TicketCategoryPart : ContentPart
{
    /// <summary>
    /// Gets or sets the desk a new ticket of the category goes to.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the team a new ticket of the category goes to.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the priority a new ticket of the category starts with.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the service-level policy of the category's tickets, after the account's.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }

    /// <summary>
    /// Gets or sets whether customers can't choose the category (or those under it) in the portal and the web form.
    /// </summary>
    public bool HiddenFromCustomers { get; set; }

    /// <summary>
    /// Gets or sets what customers read about the category when they choose it.
    /// </summary>
    public string CustomerDescription { get; set; }
}
