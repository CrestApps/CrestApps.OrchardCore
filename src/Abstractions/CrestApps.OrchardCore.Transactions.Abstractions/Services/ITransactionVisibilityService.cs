using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Decides which transactions a signed-in user may see as their own: those they own, and those owed by a party they have
/// <see cref="Parties.Models.PartyAccess.Billing"/> for, such as an account they are an admin of. The customer pages of the
/// ledger and of the documents posted to it (an invoice portal) use it, and so does paying, so an account's admins see and
/// settle its debts while a plain member sees only what they own. Staff who manage transactions see them all through their
/// permission instead.
/// </summary>
public interface ITransactionVisibilityService
{
    /// <summary>
    /// Returns whether a user may see a transaction as their own.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="userId">The user's id, or <see langword="null"/> for an anonymous visitor.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the user owns it or has billing access to its payer.</returns>
    Task<bool> CanViewAsync(Transaction transaction, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a query of the transactions a user may see as their own: owned by them, or owed by a party they have billing access to.
    /// Narrow it further with the other members of the query.
    /// </summary>
    /// <param name="userId">The user's id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The query.</returns>
    Task<TransactionQuery> CreateQueryAsync(string userId, CancellationToken cancellationToken = default);
}
