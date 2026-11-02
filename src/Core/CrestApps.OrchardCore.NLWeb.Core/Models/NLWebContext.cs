namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// The conversational context of an NLWeb request.
/// </summary>
public sealed class NLWebContext
{
    /// <summary>
    /// Gets or sets the type of context. The protocol default is <c>ConversationalContext</c>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the earlier queries of the conversation, oldest first.
    /// </summary>
    public IList<string> Previous { get; set; } = [];

    /// <summary>
    /// Gets or sets a free text description of the broader context.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets what is known about the user's preferences or constraints.
    /// </summary>
    public IList<string> Memory { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the request carries any conversational context.
    /// </summary>
    public bool HasContext
        => Previous.Count > 0 || Memory.Count > 0 || !string.IsNullOrWhiteSpace(Text);
}
