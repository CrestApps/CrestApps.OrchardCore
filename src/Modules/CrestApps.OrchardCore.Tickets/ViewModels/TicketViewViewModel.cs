using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The editor of a shared ticket view.
/// </summary>
public class TicketViewViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where it is listed.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the team it is shared with.
    /// </summary>
    public string SharedWithTeamId { get; set; }

    /// <summary>
    /// Gets or sets the status categories it lists.
    /// </summary>
    public IList<TicketStatusCategory> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the desks it lists.
    /// </summary>
    public IList<string> DeskIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whose tickets it lists.
    /// </summary>
    public string Assignee { get; set; }

    /// <summary>
    /// Gets or sets how soon a timer must be due, in hours.
    /// </summary>
    public int? DueWithinHours { get; set; }

    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    public TicketSort Sort { get; set; }

    /// <summary>
    /// Gets or sets the desks to choose from.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IList<SelectListItem> Desks { get; set; } = [];
}
