using YesSql.Indexes;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.Core.Data.YesSql.Telephony.Indexes;

/// <summary>
/// Search index for <see cref="TelephonyInteraction"/> documents.
/// </summary>
public sealed class TelephonyInteractionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the logical interaction identifier.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific call identifier.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the user identifier that owns the interaction.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name that owns the interaction.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the direction of the call.
    /// </summary>
    public CallDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the interaction was an internal extension call.
    /// </summary>
    public bool IsExtension { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the call.
    /// </summary>
    public CallOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the time, in UTC, when the call started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the interaction is a voicemail.
    /// </summary>
    public bool IsVoicemail { get; set; }

    /// <summary>
    /// Gets or sets the time, in UTC, when the voicemail was read. Null while the voicemail is unread.
    /// </summary>
    public DateTime? VoicemailReadUtc { get; set; }
}
