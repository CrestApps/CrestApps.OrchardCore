using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Certifications.Models;

/// <summary>
/// The competencies a course or a learning path grants on completion: Skills skills given to the learner at a level.
/// A competency framework is a skill category, so a competency is a skill.
/// </summary>
public sealed class CompetenciesPart : ContentPart
{
    /// <summary>
    /// The level a competency is granted at when none is set: competent, on the Skills scale of 1 to 5.
    /// </summary>
    public const int DefaultProficiency = 3;

    /// <summary>
    /// Gets or sets the ids of the skills granted.
    /// </summary>
    public List<string> SkillIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the level, from 1 (trainee) to 5 (expert), the skills are granted at.
    /// </summary>
    public int Proficiency { get; set; } = DefaultProficiency;
}
