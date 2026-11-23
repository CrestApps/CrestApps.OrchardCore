using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// The accounts of an item, from <c>/accounts/get</c>.
/// </summary>
public sealed class PlaidAccountsResponse
{
    /// <summary>
    /// Gets or sets every account of the item the person shared.
    /// </summary>
    [JsonPropertyName("accounts")]
    public List<PlaidAccount> Accounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the item.
    /// </summary>
    [JsonPropertyName("item")]
    public PlaidItemInfo Item { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the request.
    /// </summary>
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; }
}
