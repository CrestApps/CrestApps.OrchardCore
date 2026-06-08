using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for the TryAgain subject action fields.
/// </summary>
public class TryAgainSubjectActionViewModel
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts allowed.
    /// </summary>
    public int? MaxAttempt { get; set; }

    /// <summary>
    /// Gets or sets the urgency level for the retry activity.
    /// </summary>
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the normalized username to assign the retry activity to.
    /// </summary>
    public string NormalizedUserName { get; set; }

    /// <summary>
    /// Gets or sets the default number of hours to schedule ahead.
    /// </summary>
    public int? DefaultScheduleHours { get; set; }

    /// <summary>
    /// Gets or sets the selected users for assignment.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SelectedUsers { get; set; }
}
