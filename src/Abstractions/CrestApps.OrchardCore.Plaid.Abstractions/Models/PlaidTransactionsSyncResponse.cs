
namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// One page of <c>/transactions/sync</c>: what was added, changed and removed since the cursor.
/// </summary>
public sealed class PlaidTransactionsSyncResponse
{
    /// <summary>
    /// Gets or sets the new transactions.
    /// </summary>
    public List<PlaidTransaction> Added { get; set; } = [];

    /// <summary>
    /// Gets or sets the changed transactions.
    /// </summary>
    public List<PlaidTransaction> Modified { get; set; } = [];

    /// <summary>
    /// Gets or sets the removed transactions.
    /// </summary>
    public List<PlaidRemovedTransaction> Removed { get; set; } = [];

    /// <summary>
    /// Gets or sets the accounts of the item, with their balances.
    /// </summary>
    public List<PlaidAccount> Accounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the cursor to send for the next page. Keep it once every page, up to the one whose
    /// <see cref="HasMore"/> is <see langword="false"/>, was stored.
    /// </summary>
    public string NextCursor { get; set; }

    /// <summary>
    /// Gets or sets whether more pages follow.
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// Gets or sets how far Plaid's first and historical pulls are, such as <c>INITIAL_UPDATE_COMPLETE</c>.
    /// </summary>
    public string TransactionsUpdateStatus { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the request.
    /// </summary>
    public string RequestId { get; set; }
}
