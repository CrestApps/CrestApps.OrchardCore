using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.ViewModels;

public class OmnichannelActivityViewModel
{
    public string DispositionId { get; set; }

    public DateTime? ScheduleDate { get; set; }

    [BindNever]
    public IEnumerable<OmnichannelDisposition> Dispositions { get; set; }
}
