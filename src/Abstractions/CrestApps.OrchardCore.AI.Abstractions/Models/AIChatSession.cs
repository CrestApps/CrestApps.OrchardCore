using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIChatSession : Entity
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
    /// Gets or sets the collection of prompts sent during the session.
    /// Each prompt is stored as an instance of the <see cref="AIChatSessionPrompt"/> class.
    /// The list is initialized as an empty list by default.
    /// </summary>
    public IList<AIChatSessionPrompt> Prompts { get; set; } = [];

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
    public IList<ChatInteractionDocumentInfo> Documents { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC date and time when the session was first created.
    /// This property helps track the start time of the session in a standardized format (UTC).
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
