namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a shift trade stands.
/// </summary>
public enum ShiftTradeStatus
{
    /// <summary>
    /// Offered, waiting for a colleague to accept.
    /// </summary>
    Offered,

    /// <summary>
    /// A colleague accepted; waiting for a manager.
    /// </summary>
    Accepted,

    /// <summary>
    /// Done: the shifts changed hands.
    /// </summary>
    Approved,

    /// <summary>
    /// A manager turned it down.
    /// </summary>
    Declined,

    /// <summary>
    /// The colleague it was offered to turned it down.
    /// </summary>
    Rejected,

    /// <summary>
    /// The worker who offered it took it back, or a shift in it changed.
    /// </summary>
    Canceled,
}
