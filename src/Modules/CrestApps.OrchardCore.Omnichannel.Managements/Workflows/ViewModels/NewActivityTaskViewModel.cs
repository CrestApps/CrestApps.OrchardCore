using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;

public class NewActivityTaskViewModel
{
    public string CampaignId { get; set; }

    public string SubjectContentType { get; set; }

    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    public string NormalizedUserName { get; set; }

    public int? DefaultScheduleHours { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Campaigns { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Users { get; set; }
}
