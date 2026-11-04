using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// The last event of a response.
/// </summary>
public sealed class NLWebCompletedEvent : NLWebEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebCompletedEvent"/> class.
    /// </summary>
    /// <param name="totalItems">The number of results.</param>
    /// <param name="processingTimeMs">How long the answer took, in milliseconds.</param>
    /// <param name="sessionContext">The session context the caller should send back with its next request.</param>
    public NLWebCompletedEvent(
        int totalItems,
        long processingTimeMs,
        JsonObject sessionContext)
    {
        TotalItems = totalItems;
        ProcessingTimeMs = processingTimeMs;
        SessionContext = sessionContext;
    }

    /// <summary>
    /// Gets the number of results.
    /// </summary>
    public int TotalItems { get; }

    /// <summary>
    /// Gets how long the answer took, in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; }

    /// <summary>
    /// Gets the session context the caller should send back with its next request.
    /// </summary>
    public JsonObject SessionContext { get; }
}
