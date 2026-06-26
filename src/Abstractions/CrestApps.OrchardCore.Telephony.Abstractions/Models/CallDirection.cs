namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes the direction of a telephony call relative to the soft phone user.
/// </summary>
public enum CallDirection
{
    /// <summary>
    /// The call was placed by the soft phone user.
    /// </summary>
    Outbound = 0,

    /// <summary>
    /// The call was received by the soft phone user.
    /// </summary>
    Inbound = 1,
}
