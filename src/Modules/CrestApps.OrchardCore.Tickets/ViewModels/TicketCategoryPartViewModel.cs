using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The editor of a ticket category on its taxonomy term.
/// </summary>
public class TicketCategoryPartViewModel
{
    public string DeskId { get; set; }

    public string TeamId { get; set; }

    public string PriorityId { get; set; }

    public string ServiceLevelPolicyId { get; set; }

    public bool HiddenFromCustomers { get; set; }

    public string CustomerDescription { get; set; }

    [BindNever]
    public List<SelectListItem> Desks { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Priorities { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Policies { get; set; } = [];
}
