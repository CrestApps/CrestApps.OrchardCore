using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class EditOmnichannelActivity
{
    public DateTime? ScheduleAt { get; set; }

    public string CampaignId { get; set; }

    public string SubjectContentType { get; set; }

    public string Instructions { get; set; }

    public string UserId { get; set; }

    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Campaigns { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ContactContentTypes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Users { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; }
}
