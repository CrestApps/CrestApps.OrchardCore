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

    /// <summary>
    /// The call was placed by the platform for an automated activity, with nobody on this end of it.
    /// </summary>
    /// <remarks>
    /// Distinguished from a person dialling because the two can warrant different policy — an automated call is
    /// placed in volume, unattended, and at whatever hour the schedule reaches — not because either is exempt.
    /// </remarks>
    AutomatedVoice = 1,
}
