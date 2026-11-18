using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The ticket page's panel that creates a work order from the ticket.
/// </summary>
public class TicketWorkOrderViewModel
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets the work order types to choose from.
    /// </summary>
    public List<SelectListItem> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets the priorities to choose from.
    /// </summary>
    public List<SelectListItem> Priorities { get; set; } = [];
}
