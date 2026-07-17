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
}
