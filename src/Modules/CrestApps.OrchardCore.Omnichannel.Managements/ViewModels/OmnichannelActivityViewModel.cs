using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for omnichannel activity.
/// </summary>
public class OmnichannelActivityViewModel
{
    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the disposition id.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets the schedule date.
    /// </summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? ScheduleDate { get; set; }

    /// <summary>
    /// Gets or sets the campaign title.
    /// </summary>
    [BindNever]
    public string CampaignTitle { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    [BindNever]
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the interaction type.
    /// </summary>
    [BindNever]
    public string InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the instructions.
    /// </summary>
    [BindNever]
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    [BindNever]
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the scheduled local.
    /// </summary>
    [BindNever]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime ScheduledLocal { get; set; }

    /// <summary>
    /// Gets or sets the assigned to name.
    /// </summary>
    [BindNever]
    public string AssignedToName { get; set; }

    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    [BindNever]
    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the completed local.
    /// </summary>
    [BindNever]
    public DateTime? CompletedLocal { get; set; }

    /// <summary>
    /// Gets or sets the completed by name.
    /// </summary>
    [BindNever]
    public string CompletedByName { get; set; }

    /// <summary>
    /// Gets or sets the dispositions.
    /// </summary>
    [BindNever]
    public IEnumerable<OmnichannelDisposition> Dispositions { get; set; }
}
