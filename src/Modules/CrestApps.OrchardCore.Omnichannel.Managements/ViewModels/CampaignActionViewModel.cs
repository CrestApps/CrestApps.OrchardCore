using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class CampaignActionViewModel
{
    public string DispositionId { get; set; }

    public bool ShowCommunicationPreferences { get; set; }

    public bool? SetDoNotCall { get; set; }

    public bool? SetDoNotSms { get; set; }

    public bool? SetDoNotEmail { get; set; }

    public bool? SetDoNotChat { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Dispositions { get; set; }
}
