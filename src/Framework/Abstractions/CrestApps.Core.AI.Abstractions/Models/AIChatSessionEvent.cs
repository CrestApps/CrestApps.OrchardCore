namespace CrestApps.Core.AI.Models;

/// <summary>
/// Tracks a chat session event for analytics purposes.
/// One record is created per chat session to capture usage metrics.
/// </summary>
public sealed class AIChatSessionEvent : ExtensibleEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the chat session this event is associated with.
    /// </summary>
    public string SessionId { get; set; }
    /// <summary>
    /// Gets or sets the AI profile identifier used in this session.
    /// </summary>
    public string ProfileId { get; set; }
    /// <summary>
    /// Gets or sets the persistent anonymous visitor identifier.
    /// Generated on the client side and stored in localStorage for cross-session tracking.
    /// </summary>
    public string VisitorId { get; set; }
    /// <summary>
    /// Gets or sets the authenticated user identifier, if available.
    /// </summary>
    public string UserId { get; set; }
    /// <summary>
    /// Gets or sets whether the user was authenticated during this session.
    /// </summary>
    public bool IsAuthenticated { get; set; }
    /// <summary>
    /// Gets or sets the UTC timestamp when the session started.
    /// </summary>
    public DateTime SessionStartedUtc { get; set; }
    /// <summary>
    /// Gets or sets the UTC timestamp when the session ended.
    /// Null if the session is still active.
    /// </summary>
    public DateTime? SessionEndedUtc { get; set; }
    /// <summary>
    /// Gets or sets the total number of messages exchanged in this session (user + assistant).
    /// </summary>
    public int MessageCount { get; set; }
    /// <summary>
    /// Gets or sets the total handle time in seconds (duration from first to last message).
    /// </summary>
    public double HandleTimeSeconds { get; set; }
    /// <summary>
    /// Gets or sets whether the session was resolved within the chat (natural ending)
    /// versus abandoned (closed due to inactivity).
    /// Used to calculate containment rate.
    /// </summary>
    public bool IsResolved { get; set; }
    /// <summary>
    /// Gets or sets the total number of input tokens consumed across all completions in this session.
    /// </summary>
    public int TotalInputTokens { get; set; }
    /// <summary>
    /// Gets or sets the total number of output tokens generated across all completions in this session.
    /// </summary>
    public int TotalOutputTokens { get; set; }
    /// <summary>
    /// Gets or sets the average AI response latency in milliseconds across all completions in this session.
    /// </summary>
    public double AverageResponseLatencyMs { get; set; }
    /// <summary>
    /// Gets or sets the number of assistant responses that contributed to <see cref="AverageResponseLatencyMs"/>.
    /// </summary>
    public int CompletionCount { get; set; }
    /// <summary>
    /// Gets or sets the user's feedback rating for this session.
    /// Null means no feedback was provided, true means positive (thumbs up), false means negative (thumbs down).
    /// </summary>
    public bool? UserRating { get; set; }
    /// <summary>
    /// Gets or sets the total number of thumbs-up ratings across all messages in this session.
    /// </summary>
    public int ThumbsUpCount { get; set; }
    /// <summary>
    /// Gets or sets the total number of thumbs-down ratings across all messages in this session.
    /// </summary>
    public int ThumbsDownCount { get; set; }
    /// <summary>
    /// Gets or sets the aggregate conversion score across all goals.
    /// Null if conversion metrics are not enabled.
    /// </summary>
    public int? ConversionScore { get; set; }
    /// <summary>
    /// Gets or sets the maximum possible conversion score across all goals.
    /// Null if conversion metrics are not enabled.
    /// </summary>
    public int? ConversionMaxScore { get; set; }
    /// <summary>
    /// Gets or sets the individual goal results from AI evaluation.
    /// </summary>
    public List<ConversionGoalResult> ConversionGoalResults { get; set; } = [];
    /// <summary>
    /// Gets or sets the UTC timestamp when this event record was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
