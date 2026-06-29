namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes the set of operations a telephony provider supports. Used by the soft phone UI to
/// enable or disable controls based on the active provider's capabilities.
/// </summary>
[Flags]
public enum TelephonyCapabilities
{
    /// <summary>
    /// The provider supports no operations.
    /// </summary>
    None = 0,

    /// <summary>
    /// The provider can place outbound calls.
    /// </summary>
    Dial = 1,

    /// <summary>
    /// The provider can hang up active calls.
    /// </summary>
    Hangup = 1 << 1,

    /// <summary>
    /// The provider can place a call on hold.
    /// </summary>
    Hold = 1 << 2,

    /// <summary>
    /// The provider can resume a call that is on hold.
    /// </summary>
    Resume = 1 << 3,

    /// <summary>
    /// The provider can mute and unmute the local audio of a call.
    /// </summary>
    Mute = 1 << 4,

    /// <summary>
    /// The provider can transfer a call to another destination.
    /// </summary>
    Transfer = 1 << 5,

    /// <summary>
    /// The provider can merge two calls into a conference.
    /// </summary>
    Merge = 1 << 6,

    /// <summary>
    /// The provider can send DTMF digits during a call.
    /// </summary>
    SendDigits = 1 << 7,

    /// <summary>
    /// The provider can receive inbound calls.
    /// </summary>
    ReceiveCalls = 1 << 8,

    /// <summary>
    /// The provider can send a ringing inbound call to voicemail.
    /// </summary>
    Voicemail = 1 << 9,
}
