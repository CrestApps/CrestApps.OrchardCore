using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// One skill a person holds, how well, and from when to when.
/// </summary>
public sealed class SkillAssignment : CatalogItem
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
    /// Gets or sets how well the skill is held, from <see cref="SkillProficiency.Minimum"/> to
    /// <see cref="SkillProficiency.Maximum"/>.
    /// </summary>
    public int Proficiency { get; set; } = SkillProficiency.Default;

    /// <summary>
    /// Gets or sets the first day the skill counts, or <see langword="null"/> when it always has.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day the skill counts, or <see langword="null"/> when it doesn't end.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Gets or sets who gave the skill.
    /// </summary>
    public string AssignedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the skill was given or last changed.
    /// </summary>
    public DateTime AssignedUtc { get; set; }

    /// <summary>
    /// Gets the subject that holds the skill.
    /// </summary>
    public SkillSubject Subject => new(SubjectKind, SubjectId);

    /// <summary>
    /// Gets whether the skill counts on a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when the day is inside the effective dates.</returns>
    public bool IsEffectiveOn(DateOnly date)
    {
        return (EffectiveFrom is null || EffectiveFrom <= date) && (EffectiveTo is null || EffectiveTo >= date);
    }
}
