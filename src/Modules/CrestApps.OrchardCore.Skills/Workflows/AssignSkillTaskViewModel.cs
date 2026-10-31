namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// The editor of the Assign Skill workflow task.
/// </summary>
public class AssignSkillTaskViewModel
{
    /// <summary>
    /// Gets or sets who holds the skill, a Liquid expression.
    /// </summary>
    public string SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the user id or worker id, a Liquid expression.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the skill, by name or id, a Liquid expression.
    /// </summary>
    public string Skill { get; set; }

    /// <summary>
    /// Gets or sets the proficiency, a Liquid expression.
    /// </summary>
    public string Proficiency { get; set; }
}
