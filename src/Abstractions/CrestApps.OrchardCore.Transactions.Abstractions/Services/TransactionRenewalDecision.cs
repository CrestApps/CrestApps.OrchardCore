namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// What an <see cref="ITransactionRenewalFilter"/> decides about the next cycle of a recurring transaction.
/// </summary>
public enum TransactionRenewalDecision
{
    /// <summary>
    /// The next cycle is invoiced.
    /// </summary>
    Renew = 0,

    /// <summary>
    /// The next cycle is not invoiced now, and is asked about again on the next sweep. For example the agreement is still
    /// waiting for its first payment, or it is paused.
    /// </summary>
    Hold = 1,

    /// <summary>
    /// The agreement has ended, so the schedule is stopped for good.
    /// </summary>
    Stop = 2,
}
