namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// The first event of a response.
/// </summary>
public sealed class NLWebStartedEvent : NLWebEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebStartedEvent"/> class.
    /// </summary>
    /// <param name="conversationId">The identifier of the conversation, when the caller sent one.</param>
    /// <param name="queryId">The identifier of the query.</param>
    public NLWebStartedEvent(
        string conversationId,
        string queryId)
    {
        ConversationId = conversationId;
        QueryId = queryId;
    }

    /// <summary>
    /// Gets the identifier of the conversation, when the caller sent one.
    /// </summary>
    public string ConversationId { get; }

    /// <summary>
    /// Gets the identifier of the query.
    /// </summary>
    public string QueryId { get; }
}
