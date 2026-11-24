using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// A desk's priority matrix: the priority of each impact and urgency.
/// </summary>
public class TicketDeskPriorityMatrixViewModel
{
    /// <summary>
    /// Gets or sets the priority of each cell, at the cell's index (see
    /// <c>TicketDeskPriorityMatrixDisplayDriver.IndexOf</c>); empty leaves the priority as it is.
    /// </summary>
    public List<string> Cells { get; set; } = [];

    /// <summary>
    /// Gets or sets the priorities to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Priorities { get; set; } = [];
}
