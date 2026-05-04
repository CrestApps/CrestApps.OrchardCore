using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for omnichannel activity batch.
/// </summary>
public class OmnichannelActivityBatchViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the schedule at.
    /// </summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? ScheduleAt { get; set; }

    /// <summary>
    /// Gets or sets the campaign id.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the contact content type.
    /// </summary>
    public string ContactContentType { get; set; }

    /// <summary>
    /// Gets or sets the instructions.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the user ids.
    /// </summary>
    public string[] UserIds { get; set; }

    /// <summary>
    /// Gets or sets the include do no calls.
    /// </summary>
    public bool IncludeDoNoCalls { get; set; }

    /// <summary>
    /// Gets or sets the include do no sms.
    /// </summary>
    public bool IncludeDoNoSms { get; set; }

    /// <summary>
    /// Gets or sets the include do no email.
    /// </summary>
    public bool IncludeDoNoEmail { get; set; }

    /// <summary>
    /// Gets or sets the prevent duplicates.
    /// </summary>
    public bool PreventDuplicates { get; set; }

    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the lead created from.
    /// </summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? LeadCreatedFrom { get; set; }

    /// <summary>
    /// Gets or sets the lead created to.
    /// </summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? LeadCreatedTo { get; set; }

    /// <summary>
    /// Gets or sets the only published leads.
    /// </summary>
    public bool OnlyPublishedLeads { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [BindNever]
    public OmnichannelActivityBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the campaigns.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Campaigns { get; set; }

    /// <summary>
    /// Gets or sets the subject content types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the contact content types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ContactContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the users.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Users { get; set; }

    /// <summary>
    /// Gets or sets the urgency levels.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; }
}
