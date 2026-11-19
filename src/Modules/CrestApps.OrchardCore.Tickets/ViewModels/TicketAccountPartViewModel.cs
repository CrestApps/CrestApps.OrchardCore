using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

public class TicketAccountPartViewModel
{
    public string ServiceLevelPolicyId { get; set; }

    [BindNever]
    public List<SelectListItem> Policies { get; set; } = [];
}
