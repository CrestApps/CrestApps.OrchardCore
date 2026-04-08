namespace CrestApps.Core.AI.Models;

/// <summary>
/// Represents the processing status of post-session tasks for a chat session.
/// </summary>
public enum PostSessionProcessingStatus
{
    /// <summary>
    /// No post-session processing is needed or has not been initiated.
    /// </summary>
    None,

    /// <summary>
    /// Post-session processing is pending and waiting to be executed.
    /// </summary>
    Pending,

    /// <summary>
    /// Post-session processing completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Post-session processing failed after exhausting all retry attempts.
    /// </summary>
    Failed,
}
