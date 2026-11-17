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
    /// Makes a link token for Auth, United States checking and savings accounts only.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="request">Who and where.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The token.</returns>
    public Task<PlaidLinkTokenResponse> CreateLinkTokenAsync(PlaidCredentials credentials, PlaidLinkTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.ClientUserId);

        var body = new JsonObject
        {
            ["client_name"] = Truncate(string.IsNullOrWhiteSpace(request.ClientName) ? "Payments" : request.ClientName.Trim(), 30),
            ["language"] = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language,
            ["country_codes"] = new JsonArray("US"),
            ["user"] = new JsonObject { ["client_user_id"] = request.ClientUserId },
            ["products"] = new JsonArray("auth"),
            ["account_filters"] = new JsonObject
            {
                ["depository"] = new JsonObject { ["account_subtypes"] = new JsonArray("checking", "savings") },
            },
        };

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
                error = await response.Content.ReadFromJsonAsync<PlaidError>(cancellationToken);
            }
            catch (JsonException)
            {
                // Not Plaid's error shape, such as a proxy's page; the status says enough.
            }

            throw new PlaidApiException(response.StatusCode, error);
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new PlaidApiException(response.StatusCode, new PlaidError { ErrorType = "API_ERROR", ErrorCode = "EMPTY_RESPONSE" });
    }

    private static string Truncate(string value, int length)
        => value.Length <= length ? value : value[..length];
}
