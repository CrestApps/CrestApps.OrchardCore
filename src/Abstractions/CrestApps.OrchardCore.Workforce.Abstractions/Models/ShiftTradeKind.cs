namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What a shift trade does.
/// </summary>
public enum ShiftTradeKind
{
    /// <summary>
    /// The worker gives the shift to a colleague, or to anyone eligible who picks it up.
    /// </summary>
    GiveAway,

    /// <summary>
    /// Two workers exchange shifts.
    /// </summary>
    Swap,
}
