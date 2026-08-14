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
}
