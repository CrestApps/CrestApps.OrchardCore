using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// A transaction of <c>/transactions/sync</c>. Plaid's amount is positive when money leaves the account.
/// </summary>
public sealed class PlaidTransaction
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

    /// <summary>
    /// Gets or sets the amount, positive when money leaves the account.
    /// </summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO 4217 currency code, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("iso_currency_code")]
    public string IsoCurrencyCode { get; set; }

    /// <summary>
    /// Gets or sets the bank's own currency code, when <see cref="IsoCurrencyCode"/> is <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("unofficial_currency_code")]
    public string UnofficialCurrencyCode { get; set; }

    /// <summary>
    /// Gets or sets the date the transaction posted, or the date it was made while it is pending.
    /// </summary>
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the date the transaction was made, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("authorized_date")]
    public DateOnly? AuthorizedDate { get; set; }

    /// <summary>
    /// Gets or sets Plaid's cleaned-up name of the transaction.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the merchant's name, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("merchant_name")]
    public string MerchantName { get; set; }

    /// <summary>
    /// Gets or sets the bank's own description, when it was asked for.
    /// </summary>
    [JsonPropertyName("original_description")]
    public string OriginalDescription { get; set; }

    /// <summary>
    /// Gets or sets the check number, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("check_number")]
    public string CheckNumber { get; set; }

    /// <summary>
    /// Gets or sets whether the transaction is still pending. A pending transaction comes again under another id when
    /// it posts.
    /// </summary>
    [JsonPropertyName("pending")]
    public bool Pending { get; set; }

    /// <summary>
    /// Gets or sets the id of the pending transaction this posted one replaces, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("pending_transaction_id")]
    public string PendingTransactionId { get; set; }

    /// <summary>
    /// Gets or sets how the transaction was made, such as <c>online</c>, <c>in store</c> or <c>other</c>.
    /// </summary>
    [JsonPropertyName("payment_channel")]
    public string PaymentChannel { get; set; }
}
