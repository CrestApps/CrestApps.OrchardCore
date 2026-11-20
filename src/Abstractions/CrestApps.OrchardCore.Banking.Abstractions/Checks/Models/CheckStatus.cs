namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// Where a check is. <see cref="CheckStatuses.Transitions"/> says which moves are allowed.
/// </summary>
public enum CheckStatus
{
    /// <summary>
    /// Written with its number, not printed yet.
    /// </summary>
    Issued,

    /// <summary>
    /// Printed and given to the payee.
    /// </summary>
    Printed,

    /// <summary>
    /// Cancelled. A voided check keeps its number.
    /// </summary>
    Voided,

    /// <summary>
    /// The bank was asked not to pay it.
    /// </summary>
    StopPayment,

    /// <summary>
    /// The bank paid it.
    /// </summary>
    Cleared,

    /// <summary>
    /// Replaced by another check, which it links to.
    /// </summary>
    Reissued,

    /// <summary>
    /// Too old for the bank to pay.
    /// </summary>
    Stale,
}
