namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes the SIP-over-WebSocket signaling endpoint used by the browser media adapter.
/// </summary>
public sealed class SoftPhoneSignalingConfig
{
    /// <summary>
    /// Gets or sets the secure WebSocket URL for SIP signaling.
    /// </summary>
    public string WebSocketUrl { get; set; }

    /// <summary>
    /// Gets or sets the SIP address of record assigned to the browser agent.
    /// </summary>
    public string SipUri { get; set; }

    /// <summary>
    /// Gets or sets the SIP authorization user.
    /// </summary>
    public string AuthorizationUser { get; set; }

    /// <summary>
    /// Gets or sets the display name to present in SIP signaling.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the provider signaling region browser clients should register on, when the provider supports
    /// choosing one. Empty leaves the choice to the provider, which is how every client behaved before this
    /// existed. An agent may override this for themselves in the soft phone, since the nearest edge follows where
    /// the person is rather than where the tenant was configured.
    /// </summary>
    public string Region { get; set; }
}
