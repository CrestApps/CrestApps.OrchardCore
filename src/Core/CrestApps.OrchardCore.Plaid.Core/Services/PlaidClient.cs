using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Plaid.Models;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// The Plaid calls the module makes, as plain JSON over the named <see cref="PlaidConstants.HttpClientName"/> client.
/// The client id and secret go in Plaid's headers, never in a body or a log.
/// </summary>
public sealed class PlaidClient
{
    /// <summary>
    /// Marks a request that is safe to send again when it fails on the way: the resilience pipeline retries only those.
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> RetryableKey = new("CrestApps.Plaid.Retryable");

    private const string ApiVersion = "2020-09-14";

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public PlaidClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Makes a link token. By default it is for Auth, United States checking and savings accounts only; the request's
    /// products, account filters and countries change that. With an access token, Link opens in update mode for that
    /// item, and products and filters are not sent.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="request">Who and where.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The token.</returns>
    public Task<PlaidLinkTokenResponse> CreateLinkTokenAsync(PlaidCredentials credentials, PlaidLinkTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.ClientUserId);

        var countryCodes = request.CountryCodes is { Count: > 0 } ? request.CountryCodes : ["US"];

        var body = new JsonObject
        {
            ["client_name"] = Truncate(string.IsNullOrWhiteSpace(request.ClientName) ? "Payments" : request.ClientName.Trim(), 30),
            ["language"] = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language,
            ["country_codes"] = ToJsonArray(countryCodes),
            ["user"] = new JsonObject { ["client_user_id"] = request.ClientUserId },
        };

        if (!string.IsNullOrEmpty(request.AccessToken))
        {
            body["access_token"] = request.AccessToken;
        }
        else
        {
            var products = request.Products is { Count: > 0 } ? request.Products : ["auth"];

            body["products"] = ToJsonArray(products);

            if (request.AccountFilters is { Count: > 0 })
            {
                var filters = new JsonObject();

                foreach (var (type, subtypes) in request.AccountFilters)
                {
                    if (!string.IsNullOrEmpty(type) && subtypes is { Count: > 0 })
                    {
                        filters[type] = new JsonObject { ["account_subtypes"] = ToJsonArray(subtypes) };
                    }
                }

                if (filters.Count > 0)
                {
                    body["account_filters"] = filters;
                }
            }

            if (request.TransactionsDaysRequested is { } days && products.Contains("transactions", StringComparer.Ordinal))
            {
                body["transactions"] = new JsonObject { ["days_requested"] = Math.Clamp(days, 1, 730) };
            }
        }

        if (!string.IsNullOrEmpty(request.WebhookUrl))
        {
            body["webhook"] = request.WebhookUrl;
        }

        return SendAsync<PlaidLinkTokenResponse>(credentials, "link/token/create", body, retryable: true, cancellationToken);
    }

    /// <summary>
    /// Exchanges the public token Plaid Link returned for an access token. Never retried: a public token works once.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="publicToken">The public token.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The access token and the item's id.</returns>
    public Task<PlaidPublicTokenExchangeResponse> ExchangePublicTokenAsync(PlaidCredentials credentials, string publicToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(publicToken);

        return SendAsync<PlaidPublicTokenExchangeResponse>(credentials, "item/public_token/exchange", new JsonObject { ["public_token"] = publicToken }, retryable: false, cancellationToken);
    }

    /// <summary>
    /// Reads the ACH numbers of one account of an item.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="accessToken">The item's access token.</param>
    /// <param name="accountId">Plaid's id of the account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The account and its numbers.</returns>
    public Task<PlaidAuthResponse> GetAuthAsync(PlaidCredentials credentials, string accessToken, string accountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);
        ArgumentException.ThrowIfNullOrEmpty(accountId);

        var body = new JsonObject
        {
            ["access_token"] = accessToken,
            ["options"] = new JsonObject { ["account_ids"] = new JsonArray(accountId) },
        };

        return SendAsync<PlaidAuthResponse>(credentials, "auth/get", body, retryable: true, cancellationToken);
    }

    /// <summary>
    /// Lists every account of an item, with its balances.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="accessToken">The item's access token.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The accounts and the item.</returns>
    public Task<PlaidAccountsResponse> GetAccountsAsync(PlaidCredentials credentials, string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        return SendAsync<PlaidAccountsResponse>(credentials, "accounts/get", new JsonObject { ["access_token"] = accessToken }, retryable: true, cancellationToken);
    }

    /// <summary>
    /// Reads one page of <c>/transactions/sync</c>. Safe to send again: the same cursor gives the same page.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="request">The access token, cursor and page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    public Task<PlaidTransactionsSyncResponse> SyncTransactionsAsync(PlaidCredentials credentials, PlaidTransactionsSyncRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.AccessToken);

        var body = new JsonObject
        {
            ["access_token"] = request.AccessToken,
            ["count"] = Math.Clamp(request.Count, 1, 500),
        };

        if (!string.IsNullOrEmpty(request.Cursor))
        {
            body["cursor"] = request.Cursor;
        }

        var options = new JsonObject();

        if (request.IncludeOriginalDescription)
        {
            options["include_original_description"] = true;
        }

        if (!string.IsNullOrEmpty(request.AccountId))
        {
            options["account_id"] = request.AccountId;
        }

        if (options.Count > 0)
        {
            body["options"] = options;
        }

        return SendAsync<PlaidTransactionsSyncResponse>(credentials, "transactions/sync", body, retryable: true, cancellationToken);
    }

    /// <summary>
    /// Reads a bank's name and id.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="institutionId">Plaid's id of the bank.</param>
    /// <param name="countryCodes">The two-letter country codes to search, or <see langword="null"/> for the United States.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The bank.</returns>
    public async Task<PlaidInstitution> GetInstitutionAsync(PlaidCredentials credentials, string institutionId, IEnumerable<string> countryCodes = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(institutionId);

        var codes = countryCodes?.Where(code => !string.IsNullOrWhiteSpace(code)).ToList() is { Count: > 0 } list ? list : ["US"];

        var body = new JsonObject
        {
            ["institution_id"] = institutionId,
            ["country_codes"] = ToJsonArray(codes),
        };

        var response = await SendAsync<PlaidInstitutionResponse>(credentials, "institutions/get_by_id", body, retryable: true, cancellationToken);

        return response.Institution;
    }

    /// <summary>
    /// Changes the webhook URL Plaid sends an item's webhooks to. Safe to send again: the same URL gives the same item.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="accessToken">The item's access token.</param>
    /// <param name="webhookUrl">The new webhook URL, an absolute address.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The item, with the webhook URL Plaid now holds.</returns>
    public async Task<PlaidItemInfo> UpdateItemWebhookAsync(PlaidCredentials credentials, string accessToken, string webhookUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);
        ArgumentException.ThrowIfNullOrEmpty(webhookUrl);

        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("The webhook URL must be an absolute HTTP or HTTPS address.", nameof(webhookUrl));
        }

        var body = new JsonObject
        {
            ["access_token"] = accessToken,
            ["webhook"] = uri.AbsoluteUri,
        };

        var response = await SendAsync<PlaidItemResponse>(credentials, "item/webhook/update", body, retryable: true, cancellationToken);

        return response.Item;
    }

    /// <summary>
    /// Removes an item, so its access token stops working and Plaid stops billing for it.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="accessToken">The item's access token.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    public async Task RemoveItemAsync(PlaidCredentials credentials, string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        await SendAsync<JsonObject>(credentials, "item/remove", new JsonObject { ["access_token"] = accessToken }, retryable: true, cancellationToken);
    }

    /// <summary>
    /// Reads the public key a webhook was signed with.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="keyId">The key's id, from the webhook's signature header.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The key.</returns>
    public async Task<PlaidWebhookVerificationKey> GetWebhookVerificationKeyAsync(PlaidCredentials credentials, string keyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyId);

        var response = await SendAsync<PlaidWebhookVerificationKeyResponse>(credentials, "webhook_verification_key/get", new JsonObject { ["key_id"] = keyId }, retryable: true, cancellationToken);

        return response.Key;
    }

    private async Task<T> SendAsync<T>(PlaidCredentials credentials, string path, JsonObject body, bool retryable, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(credentials);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(credentials.BaseAddress, path))
        {
            Content = JsonContent.Create(body),
        };

        request.Headers.Add("PLAID-CLIENT-ID", credentials.ClientId);
        request.Headers.Add("PLAID-SECRET", credentials.Secret);
        request.Headers.Add("Plaid-Version", ApiVersion);
        request.Options.Set(RetryableKey, retryable);

        var client = _httpClientFactory.CreateClient(PlaidConstants.HttpClientName);

        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            PlaidError error = null;

            try
            {
                error = await response.Content.ReadFromJsonAsync<PlaidError>(PlaidJsonSerializerOptions.Default, cancellationToken);
            }
            catch (JsonException)
            {
                // Not Plaid's error shape, such as a proxy's page; the status says enough.
            }

            throw new PlaidApiException(response.StatusCode, error);
        }

        return await response.Content.ReadFromJsonAsync<T>(PlaidJsonSerializerOptions.Default, cancellationToken)
            ?? throw new PlaidApiException(response.StatusCode, new PlaidError { ErrorType = "API_ERROR", ErrorCode = "EMPTY_RESPONSE" });
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
        => new([.. values.Select(value => (JsonNode)JsonValue.Create(value))]);

    private static string Truncate(string value, int length)
        => value.Length <= length ? value : value[..length];
}
