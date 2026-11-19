using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Names the page that pays a transaction when it is not the ledger's own pay page, such as the checkout of the order an
/// invoice bills: the order keeps one payment path, its own, and the transaction is settled from it. The ledger's pay
/// links (<see cref="ITransactionPayLinkService.CreatePayUrlAsync"/>), its reminders and its pay page all send the payer
/// to the first address a provider gives.
/// </summary>
public interface ITransactionPayUrlProvider
{
    /// <summary>
    /// Returns the absolute address of the page that pays <paramref name="transaction"/>, or <see langword="null"/> when
    /// the ledger's pay page does.
    /// </summary>
    /// <param name="transaction">The transaction to pay.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> GetPayUrlAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
