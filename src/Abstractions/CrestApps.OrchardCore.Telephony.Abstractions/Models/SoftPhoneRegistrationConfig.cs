namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Provides the browser soft-phone registration configuration consumed by the page-local media adapter.
/// </summary>
public sealed class SoftPhoneRegistrationConfig
{
    /// <summary>
    /// Gets or sets the technical provider name.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the SIP signaling configuration.
    /// </summary>
    public SoftPhoneSignalingConfig Signaling { get; set; }

    /// <summary>
    /// Gets or sets the short-lived SIP credential.
    /// </summary>
    public SoftPhoneCredentialConfig Credential { get; set; }

    /// <summary>
    /// Gets or sets the ICE configuration.
    /// </summary>
    public SoftPhoneIceConfig Ice { get; set; }

    /// <summary>
    /// Gets or sets the media configuration.
    /// </summary>
    public SoftPhoneMediaConfig Media { get; set; }

    /// <summary>
    /// Gets or sets the soft-phone session metadata.
    /// </summary>
    public SoftPhoneSessionConfig Session { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the browser places outbound calls itself (by sending the SIP
    /// INVITE from the registered client) rather than the server originating a leg to the browser. Providers
    /// whose platform will not accept a server-originated call to a registered WebRTC credential (Telnyx) set
    /// this so the soft phone dials directly from the browser.
    /// </summary>
    public bool ClientOriginatesCalls { get; set; }

    /// <summary>
    /// Gets or sets the caller id the browser presents on client-originated outbound calls. Required by
    /// platforms that reject a call whose origination number is not an owned number (sent as the SIP
    /// P-Asserted-Identity). Ignored when <see cref="ClientOriginatesCalls"/> is <see langword="false"/>.
    /// </summary>
    public string OutboundCallerId { get; set; }
}
