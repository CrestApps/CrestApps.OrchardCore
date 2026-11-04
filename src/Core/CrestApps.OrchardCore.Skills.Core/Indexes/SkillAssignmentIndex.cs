using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Core.Indexes;

/// <summary>
/// The YesSql index of skill assignments.
/// </summary>
public sealed class SkillAssignmentIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets what kind of record holds the skill.
    /// </summary>
    public SkillSubjectKind SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the user id or worker id that holds the skill.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the skill.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets how well the skill is held.
    /// </summary>
    public int Proficiency { get; set; }

    /// <summary>
    /// Gets or sets the first day it counts as <c>yyyyMMdd</c>, or <see langword="null"/>.
    /// </summary>
    public int? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day it counts as <c>yyyyMMdd</c>, or <see langword="null"/>.
    /// </summary>
    public int? EffectiveTo { get; set; }
}
