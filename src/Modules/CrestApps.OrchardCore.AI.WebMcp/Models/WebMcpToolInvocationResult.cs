namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The result of a WebMCP tool call.
/// </summary>
public sealed class WebMcpToolInvocationResult
{
    /// <summary>
    /// Gets the outcome of the call.
    /// </summary>
    public WebMcpToolInvocationStatus Status { get; init; }

    /// <summary>
    /// Gets the text the tool answered, or the reason the call failed.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Gets a value indicating whether the result reports a failure.
    /// </summary>
    public bool IsError => Status != WebMcpToolInvocationStatus.Succeeded;

    /// <summary>
    /// Creates the result of a tool that ran and answered.
    /// </summary>
    /// <param name="text">The text the tool answered.</param>
    /// <returns>The result.</returns>
    public static WebMcpToolInvocationResult Success(string text)
    {
        return new WebMcpToolInvocationResult
        {
            Status = WebMcpToolInvocationStatus.Succeeded,
            Text = text ?? string.Empty,
        };
    }

    /// <summary>
    /// Creates the result of a call to a tool that is not available to the user.
    /// </summary>
    /// <returns>The result.</returns>
    public static WebMcpToolInvocationResult NotFound()
    {
        return new WebMcpToolInvocationResult
        {
            Status = WebMcpToolInvocationStatus.NotFound,
            Text = "The tool is not available.",
        };
    }

    /// <summary>
    /// Creates the result of a tool that failed.
    /// </summary>
    /// <param name="text">The reason, safe to show to the agent.</param>
    /// <returns>The result.</returns>
    public static WebMcpToolInvocationResult Failure(string text)
    {
        return new WebMcpToolInvocationResult
        {
            Status = WebMcpToolInvocationStatus.Failed,
            Text = text,
        };
    }
}
