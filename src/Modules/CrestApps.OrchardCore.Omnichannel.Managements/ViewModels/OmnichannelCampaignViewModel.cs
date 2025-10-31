using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelCampaignViewModel
{
    public string DisplayText { get; set; }

    public string Description { get; set; }

    public ActivityInteractionType InteractionType { get; set; }

    public string ChannelEndpointId { get; set; }

    public string AIProfileName { get; set; }

    public string InitialOutboundPromptPattern { get; set; }

    public string Channel { get; set; }

    public SelectListItem[] Dispositions { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> AIProfiles { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> InteractionTypes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ChannelEndpoints { get; set; }
}
