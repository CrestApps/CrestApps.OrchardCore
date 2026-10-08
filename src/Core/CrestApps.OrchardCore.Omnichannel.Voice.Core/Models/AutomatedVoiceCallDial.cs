namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// When an automated voice call was last dialed, stored on its activity so a call whose provider never reported
/// back can be recognised as over.
/// </summary>
/// <remarks>
/// Nothing else on the activity says when the call was placed: the scheduled time can be hours earlier, and a
/// clone keeps the original's. Without this, a call that was dialed and never heard from again -- its answer or
/// hangup webhook lost -- could not be told apart from one that is ringing right now.
/// </remarks>
public sealed class AutomatedVoiceCallDial
{
    /// <summary>
    /// Gets or sets when the call was dialed, in UTC.
    /// </summary>
    public DateTime DialedUtc { get; set; }

    /// <summary>
    /// Gets or sets the provider's id for the dialed call.
    /// </summary>
    public string ProviderCallId { get; set; }
}
