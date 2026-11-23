using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// The balances of an account, as the bank last gave them to Plaid.
/// </summary>
public sealed class PlaidBalances
{
    /// <summary>
    /// Gets or sets the amount that can be spent or withdrawn, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("available")]
    public decimal? Available { get; set; }

    /// <summary>
    /// Gets or sets the balance, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("current")]
    public decimal? Current { get; set; }

    /// <summary>
    /// Gets or sets the credit limit, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("limit")]
    public decimal? Limit { get; set; }

    /// <summary>
    /// Gets or sets the ISO 4217 currency code, or <see langword="null"/> when the bank uses one Plaid does not know.
    /// </summary>
    [JsonPropertyName("iso_currency_code")]
    public string IsoCurrencyCode { get; set; }

    /// <summary>
    /// Gets or sets the bank's own currency code, when <see cref="IsoCurrencyCode"/> is <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("unofficial_currency_code")]
    public string UnofficialCurrencyCode { get; set; }
}
