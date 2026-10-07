namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Tells a user's soft phones where the other party of one of their calls stands. It is pushed through
/// <see cref="ITelephonyClient.RemotePartyChanged"/>, so the phone can play a ringback tone while the number rings.
/// </summary>
public sealed class TelephonyRemotePartyUpdate
{
    /// <summary>
    /// Gets or sets the identifier of the call, as the soft phone tracks it.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets where the other party stands.
    /// </summary>
    public RemotePartyState State { get; set; }
}
