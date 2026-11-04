using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Says that the skills a person holds changed.
/// </summary>
public sealed class SkillAssignmentsChangedContext
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public SkillSubject Subject { get; init; }

    /// <summary>
    /// Gets or sets every skill the person holds after the change.
    /// </summary>
    public IReadOnlyList<SkillAssignment> Assignments { get; init; } = [];

    /// <summary>
    /// Gets or sets the skills that were added or changed.
    /// </summary>
    public IReadOnlyList<SkillAssignment> Changed { get; init; } = [];

    /// <summary>
    /// Gets or sets the ids of the skills that were taken away.
    /// </summary>
    public IReadOnlyList<string> RemovedSkillIds { get; init; } = [];

    /// <summary>
    /// Gets or sets who made the change.
    /// </summary>
    public string Actor { get; init; }

    /// <summary>
    /// Gets or sets whether the change only reflects an assignment starting or ending today, with nothing saved.
    /// </summary>
    public bool EffectiveDatesOnly { get; init; }
}
