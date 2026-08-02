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
}
