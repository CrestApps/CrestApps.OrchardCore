using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for the NewActivity subject action fields.
/// </summary>
public class NewActivitySubjectActionViewModel
{
    /// <summary>
    /// Gets or sets the target subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the normalized username to assign the new activity to.
    /// </summary>
    public string NormalizedUserName { get; set; }

    /// <summary>
    /// Gets or sets the default number of hours to schedule ahead.
    /// </summary>
    public int? DefaultScheduleHours { get; set; }

    /// <summary>
    /// Gets or sets the available subject content types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the selected users for assignment.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SelectedUsers { get; set; }
}
