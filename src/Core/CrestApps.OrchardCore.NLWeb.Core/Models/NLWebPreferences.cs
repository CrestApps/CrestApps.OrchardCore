namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// How the caller of an NLWeb request wants the response.
/// </summary>
public sealed class NLWebPreferences
{
    /// <summary>
    /// Gets or sets whether the caller wants a server-sent event stream, or <see langword="null"/> when it did not
    /// say.
    /// </summary>
    public bool? Streaming { get; set; }

    /// <summary>
    /// Gets or sets the response format, such as <c>conversational_search</c>.
    /// </summary>
    public string ResponseFormat { get; set; }

    /// <summary>
    /// Gets or sets the response mode: <c>list</c>, <c>summarize</c> or <c>generate</c>.
    /// </summary>
    public string Mode { get; set; }

    /// <summary>
    /// Gets or sets the language of the response, such as <c>en</c>.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Gets or sets the kind of client, such as <c>mobile</c> or an agent name.
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the number of results the caller wants, when it said.
    /// </summary>
    public int? MaxResults { get; set; }
}
