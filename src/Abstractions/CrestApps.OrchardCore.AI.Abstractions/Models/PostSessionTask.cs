namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Defines a single post-session processing task configured on an AI profile.
/// </summary>
public sealed class PostSessionTask
{
    /// <summary>
    /// Gets or sets the unique key for this task.
    /// Must be alphanumeric with underscores only.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type of post-session processing to perform.
    /// </summary>
    public PostSessionTaskType Type { get; set; }

    /// <summary>
    /// Gets or sets the user-provided instructions for the AI model.
    /// For <see cref="PostSessionTaskType.PredefinedOptions"/>, this describes how to select from the options.
    /// For <see cref="PostSessionTaskType.Semantic"/>, this provides the full processing instructions.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets whether the AI model can select multiple options.
    /// Only applicable when <see cref="Type"/> is <see cref="PostSessionTaskType.PredefinedOptions"/>.
    /// </summary>
    public bool AllowMultipleValues { get; set; }

    /// <summary>
    /// Gets or sets the list of predefined options for this task.
    /// Required when <see cref="Type"/> is <see cref="PostSessionTaskType.PredefinedOptions"/>.
    /// Each option has a value and an optional description to guide the AI model.
    /// </summary>
    public List<PostSessionTaskOption> Options { get; set; } = [];
}
