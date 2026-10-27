namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// What is kept of an AI profile's conversations once they end.
/// </summary>
public enum AIDataStorageMode
{
    /// <summary>
    /// The transcript, summaries and extracted fields are kept as they were.
    /// </summary>
    Everything,

    /// <summary>
    /// Personal details (names, addresses, phone numbers, email addresses, card and identity numbers) are replaced
    /// with placeholders in the transcript, the title, summaries, extracted fields and evaluations.
    /// </summary>
    RedactPersonalData,

    /// <summary>
    /// The transcript is deleted, and summaries, extracted values and evaluation notes are cleared. Times, status,
    /// outcomes and scores are kept.
    /// </summary>
    MetadataOnly,
}

/// <summary>
/// How long an AI profile's conversations are kept and what is kept of them.
/// </summary>
public sealed class AIProfileDataPrivacySettings
{
    /// <summary>
    /// Gets or sets what is kept of a conversation once it ends.
    /// </summary>
    public AIDataStorageMode StorageMode { get; set; }

    /// <summary>
    /// Gets or sets when the storage mode was last changed. Conversations whose last activity is later get it.
    /// </summary>
    public DateTime? StorageModeSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets after how many days without activity a conversation is deleted, or <see langword="null"/> to
    /// keep conversations.
    /// </summary>
    public int? RetentionDays { get; set; }
}

/// <summary>
/// Marks a conversation the profile's storage mode was applied to.
/// </summary>
public sealed class AIConversationPrivacyMetadata
{
    /// <summary>
    /// Gets or sets the mode applied.
    /// </summary>
    public AIDataStorageMode Mode { get; set; }

    /// <summary>
    /// Gets or sets when it was applied.
    /// </summary>
    public DateTime AppliedUtc { get; set; }
}
