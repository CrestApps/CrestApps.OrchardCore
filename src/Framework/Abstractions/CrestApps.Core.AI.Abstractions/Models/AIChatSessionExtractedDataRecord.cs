namespace CrestApps.Core.AI.Models;

/// <summary>
/// Stores the extracted-data snapshot for a single AI chat session so the values
/// can be queried without loading the live chat-session document.
/// </summary>
public sealed class AIChatSessionExtractedDataRecord : ExtensibleEntity
{
    /// <summary>
    /// Gets or sets the record identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the chat session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the AI profile identifier for the session.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time when the session started.
    /// </summary>
    public DateTime SessionStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time when the session ended, when available.
    /// </summary>
    public DateTime? SessionEndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the extracted values grouped by extraction field name.
    /// </summary>
    public Dictionary<string, List<string>> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time when the snapshot record was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
