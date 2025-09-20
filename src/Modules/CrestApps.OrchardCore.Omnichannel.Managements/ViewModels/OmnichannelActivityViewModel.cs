using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelActivityViewModel
{
    public string Notes { get; set; }

    public string DispositionId { get; set; }

    public DateTime? ScheduleDate { get; set; }

    [BindNever]
    public string CampaignTitle { get; set; }

    [BindNever]
    public string Channel { get; set; }

    [BindNever]
    public string InteractionType { get; set; }

    [BindNever]
    public string Instructions { get; set; }

    [BindNever]
    public IEnumerable<OmnichannelDisposition> Dispositions { get; set; }
}
