namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// Edits the AI Call Action workflow task.
/// </summary>
public class AICallActionTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression of the call's activity.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets what to do.
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression the action takes.
    /// </summary>
    public string Value { get; set; }
}
