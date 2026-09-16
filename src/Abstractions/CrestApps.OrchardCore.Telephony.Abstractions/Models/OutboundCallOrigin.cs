namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Identifies how an outbound origination reached the shared telephony boundary, so a screener can apply
/// the policy appropriate to that path.
/// </summary>
public enum OutboundCallOrigin
{
    /// <summary>
    /// The call was placed manually by an agent through the soft phone.
    /// </summary>
    SoftPhone = 0,
}
