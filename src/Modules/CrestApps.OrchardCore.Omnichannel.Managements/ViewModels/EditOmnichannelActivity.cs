using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the edit omnichannel activity.
/// </summary>
public class EditOmnichannelActivity
{
    /// <summary>
    /// Gets or sets the schedule at.
    /// </summary>
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
    /// Gets or sets the instructions.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    public ActivityUrgencyLevel UrgencyLevel { get; set; }

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
