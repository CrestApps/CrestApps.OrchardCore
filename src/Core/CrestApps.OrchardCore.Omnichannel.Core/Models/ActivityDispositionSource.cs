namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Identifies the source that supplied an activity disposition.
/// Workflows receive the final activity disposition without needing to know this value.
/// </summary>
public enum ActivityDispositionSource
{
    /// <summary>
    /// The disposition was selected by an agent.
    /// </summary>
    Agent,

    /// <summary>
    /// The disposition was produced by a provider event.
    /// </summary>
    Provider,

    /// <summary>
    /// The disposition was produced by AI analysis.
    /// </summary>
    AI,

    /// <summary>
    /// The disposition was produced by workflow automation.
    /// </summary>
    Workflow,

    /// <summary>
    /// The disposition was produced by a system process.
    /// </summary>
    System,
}
