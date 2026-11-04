using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The fields of blackout dates.
/// </summary>
public class BlackoutPeriodEditViewModel
{
    /// <summary>
    /// Gets or sets the id, or empty for new dates.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team, or empty for every team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the types it blocks.
    /// </summary>
    public string[] TypeIds { get; set; }

    /// <summary>
    /// Gets or sets what a worker is told.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time-off types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];
}
