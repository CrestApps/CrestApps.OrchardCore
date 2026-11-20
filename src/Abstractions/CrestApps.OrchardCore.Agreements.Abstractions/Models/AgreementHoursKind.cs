namespace CrestApps.OrchardCore.Agreements.Models;

/// <summary>
/// How an agreement covers hours of work.
/// </summary>
public enum AgreementHoursKind
{
    /// <summary>
    /// It covers no hours; time under it isn't counted against it.
    /// </summary>
    None,

    /// <summary>
    /// A block of hours bought ahead, used until it runs out, over the agreement's whole period.
    /// </summary>
    Block,

    /// <summary>
    /// An allowance of hours each calendar month, in the agreement's time zone; what is left doesn't roll over.
    /// </summary>
    MonthlyAllowance,
}
