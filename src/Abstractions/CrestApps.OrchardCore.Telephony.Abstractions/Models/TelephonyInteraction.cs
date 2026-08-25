using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a persisted telephony interaction (a call) recorded locally for history and reporting,
/// independently of the provider.
/// </summary>
public sealed class TelephonyInteraction : Entity
{
    /// <summary>
    /// Gets or sets the database primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the logical, provider-independent identifier of the interaction.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific identifier of the call.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider that handled the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who owns the interaction.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who owns the interaction.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the phone number or address that initiated the call.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the phone number or address that received the call.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the direction of the call.
    /// </summary>
    public CallDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this interaction was an internal extension call (dialed by
    /// extension rather than by phone number). The soft phone uses it to redial the entry in extension mode from
    /// the Recent tab, since <see cref="To"/> holds the target's display name, not a dialable number.
    /// </summary>
    public bool IsExtension { get; set; }

    /// <summary>
    /// Gets or sets the dialed extension number for an internal extension call (<see cref="IsExtension"/> is
    /// <see langword="true"/>), so the Recent tab's call-back button can redial the extension. It is
    /// <see langword="null"/> for calls placed by phone number.
    /// </summary>
    public string ExtensionNumber { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the call.
    /// </summary>
    public CallOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the time, in UTC, when the call started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the time, in UTC, when the call ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the duration of the call, in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this interaction is a voicemail left for the owning user (the
    /// caller was sent to voicemail and recorded a message). The soft phone surfaces these as playable voicemail
    /// entries in the history rather than plain missed calls.
    /// </summary>
    public bool IsVoicemail { get; set; }

    /// <summary>
    /// Gets or sets the time, in UTC, when the owning user first listened to (or dismissed) this voicemail. A
    /// value of <see langword="null"/> marks the voicemail as unread and counts toward the soft phone's unread
    /// voicemail badge.
    /// </summary>
    public DateTime? VoicemailReadUtc { get; set; }
}
