namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The outcome of a WebMCP tool call.
/// </summary>
public enum WebMcpToolInvocationStatus
{
    /// <summary>
    /// The tool ran and answered.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The tool does not exist, is not given to browser agents, or the user may not use it.
    /// </summary>
    NotFound,

    /// <summary>
    /// The tool ran and failed.
    /// </summary>
    Failed,
}
