namespace CrestApps.OrchardCore.Telnyx.Models;

/// <summary>
/// Site settings for the Telnyx SMS feature. The account API key and webhook public key are shared with the
/// Telnyx provider settings; these are the messaging-specific options.
/// </summary>
public sealed class TelnyxSmsSettings
{
    /// <summary>
    /// Gets or sets the optional Telnyx messaging profile identifier used when sending. Telnyx can send using
    /// only the <c>from</c> number when that number is assigned to a messaging profile; set this to send
    /// through a specific profile explicitly (for example an alphanumeric-sender or pooled-number profile).
    /// </summary>
    public string MessagingProfileId { get; set; }
}
