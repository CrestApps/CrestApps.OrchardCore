using CrestApps.Core.AI.ResponseHandling;

namespace CrestApps.Core.AI.Models;

public sealed class AIChatSession : ExtensibleEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the chat session.
    /// This property is used to track and manage the session across its lifecycle.
    /// </summary>
    public string SessionId { get; set; }
    /// <summary>
    /// Gets or sets the profile identifier associated with this chat session.
    /// It references the user's or client's profile during the session.
    /// </summary>
    public string ProfileId { get; set; }
    /// <summary>
    /// Gets or sets the title of the chat session.
    /// This can be a descriptive name or label for the session, such as "Customer Support Chat".
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// Gets or sets the user identifier who created this session.
    /// This is used to associate the session with a specific user. If unavailable, <see cref="ClientId"/> is used instead.
    /// </summary>
    public string UserId { get; set; }
    /// <summary>
    /// Gets or sets the client identifier who created this session when <see cref="UserId"/> is not available.
    /// This is typically used for cases where the session is initiated by a client or service instead of a specific user.
    /// </summary>
    public string ClientId { get; set; }
    /// <summary>
    /// Gets or sets the collection of document references attached to this session.
    /// Documents are uploaded by users and used for RAG (Retrieval-Augmented Generation).
    /// </summary>
    public List<ChatDocumentInfo> Documents { get; set; } = [];
    /// <summary>
    /// Gets or sets the UTC date and time when the session was first created.
    /// This property helps track the start time of the session in a standardized format (UTC).
    /// </summary>
    public DateTime CreatedUtc { get; set; }
    /// <summary>
    /// Gets or sets the UTC date and time of the last activity in this session.
    /// </summary>
    public DateTime LastActivityUtc { get; set; }
    /// <summary>
    /// Gets or sets the UTC date and time when the session was closed due to inactivity.
    /// </summary>
    public DateTime? ClosedAtUtc { get; set; }
    /// <summary>
    /// Gets or sets the status of the chat session.
    /// </summary>
    public ChatSessionStatus Status { get; set; }
    /// <summary>
    /// Gets or sets the technical name of the <see cref="IChatResponseHandler"/> currently
    /// handling prompts for this session. When <see langword="null"/> or empty, the default
    /// AI handler is used. This value can be changed mid-conversation (e.g., by an AI
    /// function that transfers the chat to a live-agent platform).
    /// </summary>
    public string ResponseHandlerName { get; set; }
    /// <summary>
    /// Gets or sets the extracted data fields for this session.
    /// Keys are field names from the data extraction configuration.
    /// </summary>
    public Dictionary<string, ExtractedFieldState> ExtractedData { get; set; } = [];
    /// <summary>
    /// Gets or sets the results of post-session processing tasks.
    /// Keys are task names from the post-session processing configuration.
    /// Populated after the session is closed.
    /// </summary>
    public Dictionary<string, PostSessionResult> PostSessionResults { get; set; } = [];
    /// <summary>
    /// Gets or sets the status of post-session processing for this session.
    /// </summary>
    public PostSessionProcessingStatus PostSessionProcessingStatus { get; set; }
    /// <summary>
    /// Gets or sets the number of attempts made to process post-session tasks.
    /// </summary>
    public int PostSessionProcessingAttempts { get; set; }
    /// <summary>
    /// Gets or sets the UTC timestamp of the last post-session processing attempt.
    /// </summary>
    public DateTime? PostSessionProcessingLastAttemptUtc { get; set; }
    /// <summary>
    /// Gets or sets whether post-session tasks (custom AI tasks) have been processed.
    /// Used to track partial completion so successful steps are not re-run on retry.
    /// </summary>
    public bool IsPostSessionTasksProcessed { get; set; }
    /// <summary>
    /// Gets or sets whether analytics events (resolution detection and session-end metrics)
    /// have been recorded. Used to track partial completion so successful steps are not re-run on retry.
    /// </summary>
    public bool IsAnalyticsRecorded { get; set; }
    /// <summary>
    /// Gets or sets whether conversion goals have been evaluated.
    /// Tracked independently from analytics so each step can be retried without re-running the other.
    /// </summary>
    public bool IsConversionGoalsEvaluated { get; set; }
}
