using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Creates and reads the signed links that let a transaction's owner pay it without signing in, for example from a
/// payment reminder. A link names one transaction, cannot be altered, and expires; it opens that transaction's pay
/// page and nothing else.
/// </summary>
public interface ITransactionPayLinkService
{
    /// <summary>
    /// Creates the absolute address of the pay page for <paramref name="transaction"/>, or returns
    /// <see langword="null"/> when the site's address is not known (no request, and no base URL in the site settings).
    /// </summary>
    /// <param name="transaction">The transaction to pay.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> CreatePayUrlAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the transaction a pay link was created for, or returns <see langword="null"/> when the token was altered,
    /// expired, or was not made by this site.
    /// </summary>
    /// <param name="token">The token from the pay link.</param>
    string GetTransactionId(string token);
}
