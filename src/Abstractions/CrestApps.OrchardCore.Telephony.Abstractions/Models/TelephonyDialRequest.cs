namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a server-initiated request asking a connected soft phone client to place an outbound call.
/// It is pushed to the client through <see cref="ITelephonyClient.DialRequested"/> when an operator starts a
/// call from outside the soft phone surface itself, such as the "call" button next to a phone-number field. The
/// client decides how to place the call (registering if needed, or holding an active call first), so no call
/// control is performed server-side here.
/// </summary>
public sealed class TelephonyDialRequest
{
    /// <summary>
    /// Gets or sets the destination phone number the client should dial.
    /// </summary>
    public string Number { get; set; }
}
