using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// The metadata of an NLWeb request.
/// </summary>
public sealed class NLWebMeta
{
    /// <summary>
    /// Gets or sets the protocol version the caller uses.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the conversation.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the query, used by the original protocol.
    /// </summary>
    public string QueryId { get; set; }

    /// <summary>
    /// Gets or sets the session context the site returned in an earlier response, sent back as it was.
    /// </summary>
    public JsonObject SessionContext { get; set; }
}
