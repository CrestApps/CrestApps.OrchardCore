namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Where an AI test run stands.
/// </summary>
public enum AITestRunStatus
{
    /// <summary>
    /// Waiting for the background runner.
    /// </summary>
    Queued,

    /// <summary>
    /// Running now.
    /// </summary>
    Running,

    /// <summary>
    /// The judge passed the conversation.
    /// </summary>
    Passed,

    /// <summary>
    /// The judge failed the conversation.
    /// </summary>
    Failed,

    /// <summary>
    /// The run could not finish, such as when no deployment could answer.
    /// </summary>
    Error,
}
