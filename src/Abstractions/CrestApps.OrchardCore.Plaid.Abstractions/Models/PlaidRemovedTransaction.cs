using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// A transaction Plaid removed since the last cursor.
/// </summary>
public sealed class PlaidRemovedTransaction
{
    /// <summary>
    /// Gets or sets Plaid's id of the transaction.
    /// </summary>
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the account.
    /// </summary>
    [JsonPropertyName("account_id")]
    public string AccountId { get; set; }
}
