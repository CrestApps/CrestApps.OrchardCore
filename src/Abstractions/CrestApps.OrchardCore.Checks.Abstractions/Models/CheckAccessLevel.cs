namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// What a person wants to do with the checks of a source.
/// </summary>
public enum CheckAccessLevel
{
    /// <summary>
    /// See the checks: the register, the Check Register data set and the register export.
    /// </summary>
    View,

    /// <summary>
    /// Act on the checks: print, reprint, void, stop, mark cleared and reissue.
    /// </summary>
    Manage,
}
