using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The editor of a rotation.
/// </summary>
public class RotationEditViewModel
{
    /// <summary>
    /// Gets or sets the rotation id; empty for a new rotation.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets how many weeks the rotation lasts.
    /// </summary>
    public int WeekCount { get; set; }

    /// <summary>
    /// Gets or sets the template of each day, week by week from Monday; empty for a day off.
    /// </summary>
    public string[] Slots { get; set; }

    /// <summary>
    /// Gets or sets the workers who follow the rotation.
    /// </summary>
    public string[] WorkerIds { get; set; }

    /// <summary>
    /// Gets or sets the day newly added workers start week 1 of the rotation.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Gets or sets whether the rotation is used.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the teams to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the shift templates to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TemplateOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers of the team to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> WorkerOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers following the rotation, with the day each started.
    /// </summary>
    [BindNever]
    public IList<RotationWorkerViewModel> Workers { get; set; } = [];
}
