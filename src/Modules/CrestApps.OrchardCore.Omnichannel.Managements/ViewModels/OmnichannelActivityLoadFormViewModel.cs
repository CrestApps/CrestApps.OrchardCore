using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelActivityLoadFormViewModel
{
    public string Channel { get; set; }

    public ActivityInteractionType InteractionType { get; set; }

    public string CampaignId { get; set; }

    public string SubjectContentType { get; set; }

    public string ContactContentType { get; set; }

    public string ChannelEndpoint { get; set; }

    public string AIProfileName { get; internal set; }

    public string[] UserIds { get; set; }

    public bool IncludeDoNoCalls { get; set; }

    public bool IncludeDoNoSms { get; set; }

    public bool IncludeDoNoEmail { get; set; }

    [BindNever]
    public OmnichannelActivityBatchStatus Status { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Campaigns { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ContactContentTypes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Users { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> InteractionTypes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ChannelEndpoints { get; set; }

    [BindNever]
    public IOrderedEnumerable<SelectListItem> AIProfiles { get; set; }
}
