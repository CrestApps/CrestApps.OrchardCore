using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelCampaignViewModel
{
    public string DisplayText { get; set; }

    public string Description { get; set; }

    public SelectListItem[] Dispositions { get; set; }
}
