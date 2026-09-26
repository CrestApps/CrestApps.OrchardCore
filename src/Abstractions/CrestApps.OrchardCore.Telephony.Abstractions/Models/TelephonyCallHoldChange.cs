namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// An agent putting a call on hold from the soft phone, or taking it off hold.
/// </summary>
public sealed class TelephonyCallHoldChange
{
    /// <summary>
    /// Gets or sets the provider's identifier of the call.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider that owns the call, as the user's call history records it.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the interaction the user's call history records the call under, or
    /// <see langword="null"/> when the history has no entry for it.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who held or resumed the call.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call is now on hold: <see langword="true"/> for a hold,
    /// <see langword="false"/> for a resume.
    /// </summary>
    public bool IsOnHold { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the user asked for the change, at the server clock's full precision.
    /// </summary>
    public DateTime ChangedUtc { get; set; }
}
