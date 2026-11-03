namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// Natural language content of the answer, such as a summary of the results or an answer written from them.
/// </summary>
public sealed class NLWebTextEvent : NLWebEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebTextEvent"/> class.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="mode">The response mode that produced the text: <c>summarize</c> or <c>generate</c>.</param>
    public NLWebTextEvent(
        string text,
        string mode)
    {
        Text = text;
        Mode = mode;
    }

    /// <summary>
    /// Gets the text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the response mode that produced the text.
    /// </summary>
    public string Mode { get; }
}
