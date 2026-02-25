namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a single option in a <see cref="PostSessionTaskType.PredefinedOptions"/> task.
/// The AI model selects from these options when analyzing the conversation.
/// </summary>
public sealed class PostSessionTaskOption
{
    /// <summary>
    /// Gets or sets the value of this option.
    /// This is the identifier that will be stored as the result when selected.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets an optional description that provides additional context
    /// to help the AI model understand when to select this option.
    /// </summary>
    public string Description { get; set; }
}
