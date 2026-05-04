using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;

/// <summary>
/// Represents the view model for try again activity task.
/// </summary>
public class TryAgainActivityTaskViewModel
{
    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the normalized user name.
    /// </summary>
    public string NormalizedUserName { get; set; }

    /// <summary>
    /// Gets or sets the max attempt.
    /// </summary>
    public int? MaxAttempt { get; set; }

    /// <summary>
    /// Gets or sets the default schedule hours.
    /// </summary>
    public int? DefaultScheduleHours { get; set; }

    /// <summary>
    /// Gets or sets the urgency levels.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; }

    /// <summary>
    /// Gets or sets the users.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Users { get; set; }
}
