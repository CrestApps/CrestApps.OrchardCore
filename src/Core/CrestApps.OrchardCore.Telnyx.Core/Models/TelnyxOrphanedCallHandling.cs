namespace CrestApps.OrchardCore.Telnyx.Models;

/// <summary>
/// What to do about a call the provider has up that the platform has no record of.
/// </summary>
public enum TelnyxOrphanedCallHandling
{
    /// <summary>
    /// Record it and leave it connected. The default, because an orphan may still be two people having a
    /// perfectly good conversation, and ending it would be the more destructive of the two mistakes.
    /// </summary>
    Report = 0,

    /// <summary>
    /// Tell the caller what happened and hang up. For a deployment that would rather release somebody than leave
    /// them holding a call nothing can act on.
    /// </summary>
    EndCall = 1,
}
