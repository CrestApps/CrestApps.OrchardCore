using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The editor of a coverage requirement.
/// </summary>
public class CoverageEditViewModel
{
    /// <summary>
    /// Gets or sets the requirement id; empty for a new requirement.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the skill the people must have; empty for anyone on the team.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets the first day the requirement applies.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day it applies.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Gets or sets the rows, one per line.
    /// </summary>
    public string Rows { get; set; }

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
