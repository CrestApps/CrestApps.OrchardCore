using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;

public class CompletedActivityEventViewModel
{
    public string CampaignId { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Campaigns { get; set; }
}
