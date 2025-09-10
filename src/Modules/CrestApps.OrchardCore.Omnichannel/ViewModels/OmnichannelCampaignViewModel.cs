using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.ViewModels;

public class OmnichannelCampaignViewModel
{
    public string DisplayText { get; set; }

    public string Descriptions { get; set; }

    public SelectListItem[] Dispositions { get; set; }
}
