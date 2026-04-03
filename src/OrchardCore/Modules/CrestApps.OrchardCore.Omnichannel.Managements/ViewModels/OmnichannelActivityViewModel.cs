using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelActivityViewModel
{
    public string Notes { get; set; }

    public string DispositionId { get; set; }

    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
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
    public string Subject { get; set; }

    [BindNever]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime ScheduledLocal { get; set; }

    [BindNever]
    public string AssignedToName { get; set; }

    [BindNever]
    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    [BindNever]
    public DateTime? CompletedLocal { get; set; }

    [BindNever]
    public string CompletedByName { get; set; }

    [BindNever]
    public IEnumerable<OmnichannelDisposition> Dispositions { get; set; }
}
