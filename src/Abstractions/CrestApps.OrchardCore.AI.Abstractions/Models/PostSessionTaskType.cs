namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Defines the type of post-session processing task.
/// </summary>
public enum PostSessionTaskType
{
    /// <summary>
    /// Determines the outcome/result of the chat session from a predefined list of options.
    /// </summary>
    Disposition,

    /// <summary>
    /// Generates a concise summary of the chat session.
    /// </summary>
    Summary,

    /// <summary>
    /// Analyzes the overall sentiment of the conversation (Positive, Negative, Neutral).
    /// </summary>
    Sentiment,

    /// <summary>
    /// A custom task with user-provided instructions.
    /// </summary>
    Custom,
}
