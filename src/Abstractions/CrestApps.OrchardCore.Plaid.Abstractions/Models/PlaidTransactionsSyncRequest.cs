namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// One page of <c>/transactions/sync</c>.
/// </summary>
public sealed class PlaidTransactionsSyncRequest
{
    /// <summary>
    /// Gets or sets the item's access token. Never log it.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the cursor the last page returned, or <see langword="null"/> to start from the beginning.
    /// </summary>
    public string Cursor { get; set; }

    /// <summary>
    /// Gets or sets how many transactions a page holds, from 1 to 500.
    /// </summary>
    public int Count { get; set; } = 100;

    /// <summary>
    /// Gets or sets Plaid's id of one account to sync, or <see langword="null"/> for every account of the item.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets whether to return the bank's own description of each transaction.
    /// </summary>
    public bool IncludeOriginalDescription { get; set; }
}
