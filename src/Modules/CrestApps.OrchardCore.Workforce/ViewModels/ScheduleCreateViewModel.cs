using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The form that starts a team schedule.
/// </summary>
public class ScheduleCreateViewModel
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets a name; empty for one made from the team and the first day.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Gets or sets how many weeks it lasts.
    /// </summary>
    public int Weeks { get; set; } = 2;

    /// <summary>
    /// Gets or sets the teams to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];
}
