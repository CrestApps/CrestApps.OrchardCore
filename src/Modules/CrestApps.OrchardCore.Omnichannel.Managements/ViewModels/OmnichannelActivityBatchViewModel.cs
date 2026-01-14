using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelActivityBatchViewModel
{
    public string DisplayText { get; set; }

    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? ScheduleAt { get; set; }

    public string CampaignId { get; set; }

    public string SubjectContentType { get; set; }

    public string ContactContentType { get; set; }

    public string Instructions { get; set; }

    public string[] UserIds { get; set; }

    public bool IncludeDoNoCalls { get; set; }

    public bool IncludeDoNoSms { get; set; }

    public bool IncludeDoNoEmail { get; set; }

    public bool PreventDuplicates { get; set; }

    public ActivityUrgencyLevel UrgencyLevel { get; set; }

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
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; }
}
