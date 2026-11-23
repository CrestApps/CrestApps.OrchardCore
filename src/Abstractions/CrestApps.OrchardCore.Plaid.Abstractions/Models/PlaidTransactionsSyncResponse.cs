using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// One page of <c>/transactions/sync</c>: what was added, changed and removed since the cursor.
/// </summary>
public sealed class PlaidTransactionsSyncResponse
{
    /// <summary>
    /// Gets or sets the new transactions.
    /// </summary>
    [JsonPropertyName("added")]
    public List<PlaidTransaction> Added { get; set; } = [];

    /// <summary>
    /// Gets or sets the changed transactions.
    /// </summary>
    [JsonPropertyName("modified")]
    public List<PlaidTransaction> Modified { get; set; } = [];

    /// <summary>
    /// Gets or sets the removed transactions.
    /// </summary>
    [JsonPropertyName("removed")]
    public List<PlaidRemovedTransaction> Removed { get; set; } = [];

    /// <summary>
    /// Gets or sets the accounts of the item, with their balances.
    /// </summary>
    [JsonPropertyName("accounts")]
    public List<PlaidAccount> Accounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the cursor to send for the next page. Keep it once every page, up to the one whose
    /// <see cref="HasMore"/> is <see langword="false"/>, was stored.
    /// </summary>
    [JsonPropertyName("next_cursor")]
    public string NextCursor { get; set; }

    /// <summary>
    /// Gets or sets whether more pages follow.
    /// </summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    /// <summary>
    /// Gets or sets how far Plaid's first and historical pulls are, such as <c>INITIAL_UPDATE_COMPLETE</c>.
    /// </summary>
    [JsonPropertyName("transactions_update_status")]
    public string TransactionsUpdateStatus { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the request.
    /// </summary>
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; }
}
