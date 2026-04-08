namespace CrestApps.Core.AI.Models;

/// <summary>
/// Represents the processing status of an individual post-session task.
/// </summary>
public enum PostSessionTaskResultStatus
{
    /// <summary>
    /// The task has not been attempted yet.
    /// </summary>
    Pending,

    /// <summary>
    /// The task completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The task failed during execution.
    /// </summary>
    Failed,
}
