using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// What a link token is made for.
/// </summary>
public sealed class PlaidLinkTokenRequest
{
    /// <summary>
    /// Gets or sets the stable, opaque id of the person, never an e-mail address or a name.
    /// </summary>
    public string ClientUserId { get; set; }

    /// <summary>
    /// Gets or sets the name Plaid Link shows for the site.
    /// </summary>
    public string ClientName { get; set; }

    /// <summary>
    /// Gets or sets the two-letter language Plaid Link speaks.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Gets or sets the absolute URL Plaid sends webhooks to, or <see langword="null"/>.
    /// </summary>
    public string WebhookUrl { get; set; }

    /// <summary>
    /// Gets or sets the Plaid products Link asks for, such as <c>auth</c> or <c>transactions</c>. Ignored in update
    /// mode (when <see cref="AccessToken"/> is set).
    /// </summary>
    public IList<string> Products { get; set; } = ["auth"];

    /// <summary>
    /// Gets or sets the account subtypes Link offers, by account type (such as <c>depository</c>: <c>checking</c>,
    /// <c>savings</c>). Empty offers every account the products support. Ignored in update mode.
    /// </summary>
    public IDictionary<string, IList<string>> AccountFilters { get; set; } = new Dictionary<string, IList<string>>(StringComparer.Ordinal)
    {
        ["depository"] = ["checking", "savings"],
    };

    /// <summary>
    /// Gets or sets the two-letter country codes of the banks Link offers.
    /// </summary>
    public IList<string> CountryCodes { get; set; } = ["US"];

    /// <summary>
    /// Gets or sets how many days of transaction history to ask for (1 to 730), or <see langword="null"/> for Plaid's
    /// default. Used only when <see cref="Products"/> holds <c>transactions</c>.
    /// </summary>
    public int? TransactionsDaysRequested { get; set; }

    /// <summary>
    /// Gets or sets the access token of an existing item, to open Link in update mode (the person signs in again, or
    /// adds accounts), or <see langword="null"/> for a new item. Never log it.
    /// </summary>
    public string AccessToken { get; set; }
}

/// <summary>
/// A short-lived token that opens Plaid Link.
/// </summary>
public sealed class PlaidLinkTokenResponse
{
    /// <summary>
    /// Gets or sets the token.
    /// </summary>
    public string LinkToken { get; set; }

    /// <summary>
    /// Gets or sets when it expires.
    /// </summary>
    public DateTimeOffset? Expiration { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the request.
    /// </summary>
    public string RequestId { get; set; }
}

/// <summary>
/// The access token and item a public token was exchanged for.
/// </summary>
public sealed class PlaidPublicTokenExchangeResponse
{
    /// <summary>
    /// Gets or sets the access token. Never log or store it in clear text.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the item's id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the request.
    /// </summary>
    public string RequestId { get; set; }

    /// <inheritdoc />
    public override string ToString()
        => $"Item {ItemId}";
}

/// <summary>
/// The answer of <c>/auth/get</c>: the accounts and their ACH numbers.
/// </summary>
public sealed class PlaidAuthResponse
{
    /// <summary>
    /// Gets or sets the accounts.
    /// </summary>
    public List<PlaidAccount> Accounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the numbers.
    /// </summary>
    public PlaidAuthNumbers Numbers { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the request.
    /// </summary>
    public string RequestId { get; set; }
}

/// <summary>
/// An account of an item.
/// </summary>
public sealed class PlaidAccount
{
    /// <summary>
    /// Gets or sets Plaid's id of the account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the last digits the bank shows.
    /// </summary>
    public string Mask { get; set; }

    /// <summary>
    /// Gets or sets the bank's name of the account.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the bank's official name of the account, or <see langword="null"/>.
    /// </summary>
    public string OfficialName { get; set; }

    /// <summary>
    /// Gets or sets the type, such as <c>depository</c>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the subtype, such as <c>checking</c> or <c>savings</c>.
    /// </summary>
    public string Subtype { get; set; }

    /// <summary>
    /// Gets or sets how far the account's verification is, when it was not verified at once.
    /// </summary>
    public string VerificationStatus { get; set; }

    /// <summary>
    /// Gets or sets the balances, when the call returns them.
    /// </summary>
    public PlaidBalances Balances { get; set; }
}

/// <summary>
/// The numbers of <c>/auth/get</c>.
/// </summary>
public sealed class PlaidAuthNumbers
{
    /// <summary>
    /// Gets or sets the ACH numbers of United States accounts.
    /// </summary>
    public List<PlaidAchNumbers> Ach { get; set; } = [];
}

/// <summary>
/// The ACH numbers of one account.
/// </summary>
public sealed class PlaidAchNumbers
{
    /// <summary>
    /// Gets or sets Plaid's id of the account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the account number. Never log it.
    /// </summary>
    public string Account { get; set; }

    /// <summary>
    /// Gets or sets the ABA routing number.
    /// </summary>
    public string Routing { get; set; }

    /// <summary>
    /// Gets or sets whether the account number is one the bank made for Plaid, which stops working when the person
    /// takes back Plaid's access.
    /// </summary>
    public bool IsTokenizedAccountNumber { get; set; }

    /// <inheritdoc />
    public override string ToString()
        => $"Account {AccountId}";
}

/// <summary>
/// The public key Plaid signs webhooks with, as a JSON web key.
/// </summary>
public sealed class PlaidWebhookVerificationKey
{
    /// <summary>
    /// Gets or sets the algorithm, <c>ES256</c>.
    /// </summary>
    [JsonPropertyName("alg")]
    public string Algorithm { get; set; }

    /// <summary>
    /// Gets or sets the curve, <c>P-256</c>.
    /// </summary>
    [JsonPropertyName("crv")]
    public string Curve { get; set; }

    /// <summary>
    /// Gets or sets the key's id.
    /// </summary>
    [JsonPropertyName("kid")]
    public string KeyId { get; set; }

    /// <summary>
    /// Gets or sets the key type, <c>EC</c>.
    /// </summary>
    [JsonPropertyName("kty")]
    public string KeyType { get; set; }

    /// <summary>
    /// Gets or sets the x coordinate, base64url.
    /// </summary>
    public string X { get; set; }

    /// <summary>
    /// Gets or sets the y coordinate, base64url.
    /// </summary>
    public string Y { get; set; }

    /// <summary>
    /// Gets or sets when the key was made, in Unix seconds.
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the key expired, in Unix seconds, or <see langword="null"/> while it is current.
    /// </summary>
    public long? ExpiredAt { get; set; }
}

/// <summary>
/// The answer of <c>/webhook_verification_key/get</c>.
/// </summary>
public sealed class PlaidWebhookVerificationKeyResponse
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public PlaidWebhookVerificationKey Key { get; set; }
}

/// <summary>
/// An error Plaid answered with.
/// </summary>
public sealed class PlaidError
{
    /// <summary>
    /// Gets or sets the broad kind, such as <c>INVALID_INPUT</c> or <c>ITEM_ERROR</c>.
    /// </summary>
    public string ErrorType { get; set; }

    /// <summary>
    /// Gets or sets the exact code, such as <c>PRODUCT_NOT_READY</c>.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the developer message.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a message Plaid says may be shown to the person, or <see langword="null"/>.
    /// </summary>
    public string DisplayMessage { get; set; }

    /// <summary>
    /// Gets or sets Plaid's id of the request.
    /// </summary>
    public string RequestId { get; set; }
}

/// <summary>
/// A webhook Plaid sent.
/// </summary>
public sealed class PlaidWebhook
{
    /// <summary>
    /// Gets or sets the kind, such as <c>ITEM</c> or <c>AUTH</c>.
    /// </summary>
    public string WebhookType { get; set; }

    /// <summary>
    /// Gets or sets the code, such as <c>USER_PERMISSION_REVOKED</c>.
    /// </summary>
    public string WebhookCode { get; set; }

    /// <summary>
    /// Gets or sets the item's id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the account's id, for a webhook about one account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the environment that sent it.
    /// </summary>
    public string Environment { get; set; }

    /// <summary>
    /// Gets or sets the error, for an error webhook.
    /// </summary>
    public PlaidError Error { get; set; }

    /// <summary>
    /// Gets or sets how many new transactions there are, for some <c>TRANSACTIONS</c> webhooks.
    /// </summary>
    public int? NewTransactions { get; set; }

    /// <summary>
    /// Gets or sets whether the first 30 days of transactions are ready, for <c>TRANSACTIONS: SYNC_UPDATES_AVAILABLE</c>.
    /// </summary>
    public bool? InitialUpdateComplete { get; set; }

    /// <summary>
    /// Gets or sets whether the whole history is ready, for <c>TRANSACTIONS: SYNC_UPDATES_AVAILABLE</c>.
    /// </summary>
    public bool? HistoricalUpdateComplete { get; set; }
}
