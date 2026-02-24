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
    /// For Disposition type, this describes how to select from the options.
    /// For Custom type, this provides the full processing instructions.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the list of predefined options for this task.
    /// Used primarily for Disposition type (e.g., "Resolved", "Escalated", "Abandoned").
    /// </summary>
    public List<string> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this task is required for the post-session processing to be considered complete.
    /// </summary>
    public bool IsRequired { get; set; }
}
