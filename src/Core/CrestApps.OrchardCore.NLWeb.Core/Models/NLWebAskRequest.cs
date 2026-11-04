namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// An NLWeb <c>ask</c> request, independent of the protocol version and the transport it came in.
/// </summary>
public sealed class NLWebAskRequest
{
    /// <summary>
    /// Gets or sets the query: the user's question and its filters.
    /// </summary>
    public NLWebQuery Query { get; set; } = new();

    /// <summary>
    /// Gets or sets the context of the query, such as the earlier queries of the conversation.
    /// </summary>
    public NLWebContext Context { get; set; } = new();

    /// <summary>
    /// Gets or sets how the caller wants the response.
    /// </summary>
    public NLWebPreferences Prefer { get; set; } = new();

    /// <summary>
    /// Gets or sets the metadata of the request.
    /// </summary>
    public NLWebMeta Meta { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the request uses the original query-string protocol (version 0.1),
    /// whose response has a different shape.
    /// </summary>
    public bool IsLegacy { get; set; }
}
