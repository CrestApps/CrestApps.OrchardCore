using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How many of a team's people are needed at each time of the week, optionally with a skill.
/// </summary>
public sealed class CoverageRequirement : CatalogItem
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets a name for the requirement, such as Front desk.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the skill the people need, or <see langword="null"/> for anyone on the team.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets where the requirement comes from.
    /// </summary>
    public CoverageSource Source { get; set; } = CoverageSource.Manual;

    /// <summary>
    /// Gets or sets the needs by time of day.
    /// </summary>
    public IList<CoverageRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the first day it applies.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day it applies.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }
}
