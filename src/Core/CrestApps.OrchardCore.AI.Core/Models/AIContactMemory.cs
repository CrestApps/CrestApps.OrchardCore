namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// What the AI remembers about a contact from earlier conversations, on every channel and with every profile: a
/// short note rewritten after each conversation.
/// </summary>
public sealed class AIContactMemory
{
    /// <summary>
    /// The longest note kept, in characters.
    /// </summary>
    public const int MaxLength = 2000;

    /// <summary>
    /// Gets or sets the unique identifier of this record.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the contact.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets the note.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets when the note last changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the AI session the note was last rewritten from, or <see langword="null"/> when a person edited it.
    /// </summary>
    public string LastSessionId { get; set; }
}

/// <summary>
/// Whether an AI profile reads and writes contact memory.
/// </summary>
public sealed class AIProfileContactMemorySettings
{
    /// <summary>
    /// Gets or sets whether the profile's instructions get the contact's memory note.
    /// </summary>
    public bool UseMemory { get; set; }

    /// <summary>
    /// Gets or sets whether the profile's conversations rewrite the contact's memory note when they end.
    /// </summary>
    public bool SaveMemory { get; set; }
}

/// <summary>
/// The contact and omnichannel activity an AI session is with, kept on the session so work done after the
/// conversation, such as updating the contact's memory, knows who it was.
/// </summary>
public sealed class AIConversationContactMetadata
{
    /// <summary>
    /// Gets or sets the content item id of the contact.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets the omnichannel activity.
    /// </summary>
    public string ActivityId { get; set; }
}
