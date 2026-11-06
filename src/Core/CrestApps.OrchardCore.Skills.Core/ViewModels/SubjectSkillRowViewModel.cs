namespace CrestApps.OrchardCore.Skills.Core.ViewModels;

/// <summary>
/// One skill in the Skills section of an editor.
/// </summary>
public class SubjectSkillRowViewModel
{
    /// <summary>
    /// Gets or sets the skill id.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets the skill name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the skill category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the proficiency the subject holds the skill at, or 0 when it doesn't hold it.
    /// </summary>
    public int Proficiency { get; set; }
}
