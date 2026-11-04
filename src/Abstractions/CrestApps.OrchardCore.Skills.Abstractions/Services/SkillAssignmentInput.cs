using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// A skill to give someone.
/// </summary>
public sealed class SkillAssignmentInput
{
    /// <summary>
    /// Gets or sets the skill.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets how well it is held.
    /// </summary>
    public int Proficiency { get; set; } = SkillProficiency.Default;

    /// <summary>
    /// Gets or sets the first day it counts, or <see langword="null"/> for always.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day it counts, or <see langword="null"/> when it doesn't end.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }
}
