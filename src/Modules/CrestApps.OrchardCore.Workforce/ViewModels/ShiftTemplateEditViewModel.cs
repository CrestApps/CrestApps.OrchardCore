using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The editor of a shift template.
/// </summary>
public class ShiftTemplateEditViewModel
{
    /// <summary>
    /// Gets or sets the template id; empty for a new template.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets when the shift starts, as HH:mm.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets when the shift ends, as HH:mm; at or before the start for the next day.
    /// </summary>
    public string End { get; set; }

    /// <summary>
    /// Gets or sets the breaks and activities, one per line.
    /// </summary>
    public string Segments { get; set; }

    /// <summary>
    /// Gets or sets the teams that use the template; empty for every team.
    /// </summary>
    public string[] TeamIds { get; set; }

    /// <summary>
    /// Gets or sets the skills a worker needs for the shift.
    /// </summary>
    public string[] RequiredSkillIds { get; set; }

    /// <summary>
    /// Gets or sets whether the template can be used.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the names of the activity codes, for the hint.
    /// </summary>
    [BindNever]
    public IList<string> ActivityNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the skills to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> SkillOptions { get; set; } = [];
}
