namespace CrestApps.OrchardCore.Skills.ViewModels;

/// <summary>
/// One skill in the Skills section of the user editor.
/// </summary>
public class UserSkillRowViewModel
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
    /// Gets or sets the proficiency the user holds it at, or 0 when they don't hold it.
    /// </summary>
    public int Proficiency { get; set; }
}
