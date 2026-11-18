namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// A skill a piece of work needs, at a lowest proficiency. A record that can only be handled by someone with certain
/// skills (a work order, a ticket) keeps a list of these, and the people are matched against it with
/// <see cref="Services.SkillRequirementExtensions.HoldsAllAsync"/>.
/// </summary>
public sealed class SkillRequirement
{
    /// <summary>
    /// Gets or sets the skill.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets the lowest proficiency, from 1.
    /// </summary>
    public int MinProficiency { get; set; } = 1;
}
