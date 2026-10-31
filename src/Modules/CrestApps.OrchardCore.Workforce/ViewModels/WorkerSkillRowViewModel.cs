namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One skill in the worker editor.
/// </summary>
public class WorkerSkillRowViewModel
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
    /// Gets or sets the proficiency, or 0 when the worker doesn't hold it.
    /// </summary>
    public int Proficiency { get; set; }
}
