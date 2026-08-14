namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a telephony call and its current state. Instances are serialized to the soft phone
/// client over SignalR.
/// </summary>
public sealed class TelephonyCall
{
    /// <summary>
    /// Gets or sets the provider-specific identifier of the call.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the phone number or address that initiated the call.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the phone number or address that received the call.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the current lifecycle state of the call.
    /// </summary>
    public CallState State { get; set; }

    /// <summary>
    /// Gets or sets the direction of the call relative to the soft phone user.
    /// </summary>
    public CallDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the local audio of the call is currently muted.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call is currently on hold.
    /// </summary>
    public bool IsOnHold { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider that owns the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the time, in UTC, when the call started.
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets optional provider-neutral metadata associated with the call.
    /// Providers and orchestration modules can use this bag to carry routing hints or contextual
    /// data without extending the shared telephony contracts with provider-specific properties.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}
