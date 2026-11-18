namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// Who a desk serves.
/// </summary>
public enum TicketDeskAudience
{
    /// <summary>
    /// Customers, who may see their tickets in the portal.
    /// </summary>
    Customers,

    /// <summary>
    /// People inside the organization, such as an IT or HR desk.
    /// </summary>
    Internal,
}
