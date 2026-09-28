using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// An entry point's welcome or closed message on one call: which message the caller is owed, what happens to them
/// once it has been said, and how far it has got.
/// </summary>
/// <remarks>
/// It is recorded on the interaction rather than held in memory, for the same reasons as the caller's menu position:
/// the end of the message arrives as a separate provider webhook, possibly on another node or after a restart, and
/// that webhook is delivered at least once. The recorded status is what makes the message play once per call: the
/// caller is announced to only from <see cref="Scheduled"/>, and moved on only from <see cref="Speaking"/>, so a
/// redelivered or unrelated end-of-speech event, a caller who goes back to the main menu, and a caller re-routed
/// after a queue overflows are never announced to again.
/// </remarks>
public sealed class EntryPointAnnouncement
{
    /// <summary>
    /// The technical-metadata key the message kind (<see cref="Welcome"/> or <see cref="Closed"/>) is stored under.
    /// </summary>
    public const string KindMetadataKey = "entryPointAnnouncement";

    /// <summary>
    /// The technical-metadata key what happens after the message is stored under.
    /// </summary>
    public const string NextMetadataKey = "entryPointAnnouncementNext";

    /// <summary>
    /// The technical-metadata key the queue a closed caller is held in is stored under.
    /// </summary>
    public const string QueueMetadataKey = "entryPointAnnouncementQueueId";

    /// <summary>
    /// The technical-metadata key the message's progress is stored under.
    /// </summary>
    public const string StatusMetadataKey = "entryPointAnnouncementStatus";

    /// <summary>
    /// The entry point's welcome message, said to a caller who rings while it is open.
    /// </summary>
    public const string Welcome = "Welcome";

    /// <summary>
    /// The entry point's closed message, said to a caller who rings while it is closed.
    /// </summary>
    public const string Closed = "Closed";

    /// <summary>
    /// After the message the caller hears the entry point's phone menu.
    /// </summary>
    public const string NextMenu = "Menu";

    /// <summary>
    /// After the message the caller is put through to the entry point's own target, its queue or its agent.
    /// </summary>
    public const string NextTarget = "Target";

    /// <summary>
    /// After the message the caller waits in <see cref="QueueId"/>: a closed entry point that holds or overflows.
    /// </summary>
    public const string NextQueue = "Queue";

    /// <summary>
    /// After the message the caller is sent to voicemail.
    /// </summary>
    public const string NextVoicemail = "Voicemail";

    /// <summary>
    /// After the message the call is ended.
    /// </summary>
    public const string NextReject = "Reject";

    /// <summary>
    /// The routing decided the caller is owed the message; nothing has been said yet.
    /// </summary>
    public const string Scheduled = "Scheduled";

    /// <summary>
    /// The provider accepted the message and the caller is hearing it; its end moves them on.
    /// </summary>
    public const string Speaking = "Speaking";

    /// <summary>
    /// The message was said and the caller has been moved on.
    /// </summary>
    public const string Played = "Played";

    /// <summary>
    /// The message was emptied after the call arrived, so nothing was said.
    /// </summary>
    public const string Skipped = "Skipped";

    /// <summary>
    /// The provider could not say the message, so the caller was moved on without it.
    /// </summary>
    public const string Failed = "Failed";

    /// <summary>
    /// The reason recorded when a closed entry point sent the caller to voicemail.
    /// </summary>
    public const string ClosedVoicemailReasonCode = "entry_point_closed_voicemail";

    /// <summary>
    /// The reason recorded when a closed entry point ended the call.
    /// </summary>
    public const string ClosedRejectReasonCode = "entry_point_closed_reject";

    /// <summary>
    /// Gets the message kind: <see cref="Welcome"/> or <see cref="Closed"/>.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets what happens to the caller once the message has been said.
    /// </summary>
    public string Next { get; init; }

    /// <summary>
    /// Gets the queue the caller waits in when <see cref="Next"/> is <see cref="NextQueue"/>.
    /// </summary>
    public string QueueId { get; init; }

    /// <summary>
    /// Gets how far the message has got.
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Records that the caller is owed a message before they go on.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="kind"><see cref="Welcome"/> or <see cref="Closed"/>.</param>
    /// <param name="next">What happens once the message has been said.</param>
    /// <param name="queueId">The queue the caller waits in, when <paramref name="next"/> is <see cref="NextQueue"/>.</param>
    public static void Schedule(Interaction interaction, string kind, string next, string queueId = null)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        interaction.TechnicalMetadata ??= new Dictionary<string, object>();
        interaction.TechnicalMetadata[KindMetadataKey] = kind;
        interaction.TechnicalMetadata[NextMetadataKey] = next;
        interaction.TechnicalMetadata[StatusMetadataKey] = Scheduled;

        if (!string.IsNullOrEmpty(queueId))
        {
            interaction.TechnicalMetadata[QueueMetadataKey] = queueId;
        }
    }

    /// <summary>
    /// Records how far the message has got.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="status">The new status.</param>
    public static void SetStatus(Interaction interaction, string status)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        interaction.TechnicalMetadata ??= new Dictionary<string, object>();
        interaction.TechnicalMetadata[StatusMetadataKey] = status;
    }

    /// <summary>
    /// Reads the caller's message off the interaction, or <see langword="null"/> when they are owed none.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    public static EntryPointAnnouncement Read(Interaction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var kind = ReadString(interaction, KindMetadataKey);

        if (string.IsNullOrEmpty(kind))
        {
            return null;
        }

        return new EntryPointAnnouncement
        {
            Kind = kind,
            Next = ReadString(interaction, NextMetadataKey),
            QueueId = ReadString(interaction, QueueMetadataKey),
            Status = ReadString(interaction, StatusMetadataKey),
        };
    }

    // Stored as plain strings, which the store hands back as strings (or a JSON string element), never as the
    // ExpandoObject an untyped object comes back as.
    private static string ReadString(Interaction interaction, string key)
        => interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;
}
