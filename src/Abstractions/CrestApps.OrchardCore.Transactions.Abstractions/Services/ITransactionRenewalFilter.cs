using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Lets the module that owns the agreement a recurring <see cref="Transaction"/> bills decide whether its next cycle may
/// be invoiced.
/// </summary>
/// <remarks>
/// The ledger keeps the schedule for an offline agreement (Pay Later, a bank transfer), but it does not know whether the
/// agreement is still current: a subscription that is waiting for its first payment, paused, or canceled lives in
/// another module. Without asking, the renewal sweep invoiced every one of them again each cycle.
/// </remarks>
public interface ITransactionRenewalFilter
{
    /// <summary>
    /// Decides what happens to the next cycle of a recurring transaction whose period has ended.
    /// </summary>
    /// <param name="transaction">The cycle whose period has ended.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns><see cref="TransactionRenewalDecision.Renew"/> when this filter has nothing against it.</returns>
    Task<TransactionRenewalDecision> DecideAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
