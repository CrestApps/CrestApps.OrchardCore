namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// What a page tool answered in the user's browser.
/// </summary>
public sealed class WebMcpBrowserToolResult
{
    /// <summary>
    /// Gets the text the tool answered, or the reason it failed.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Gets a value indicating whether the result reports a failure.
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    /// Creates the result of a tool that answered.
    /// </summary>
    /// <param name="text">The text the tool answered.</param>
    /// <returns>The result.</returns>
    public static WebMcpBrowserToolResult Success(string text)
    {
        return new WebMcpBrowserToolResult
        {
            Text = text ?? string.Empty,
        };
    }

    /// <summary>
    /// Creates the result of a tool that failed or did not answer.
    /// </summary>
    /// <param name="text">The reason.</param>
    /// <returns>The result.</returns>
    public static WebMcpBrowserToolResult Failure(string text)
    {
        return new WebMcpBrowserToolResult
        {
            Text = text,
            IsError = true,
        };
    }
}
