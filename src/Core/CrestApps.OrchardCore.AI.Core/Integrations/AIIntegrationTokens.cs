using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Integrations;

/// <summary>
/// Signs integration connections in and keeps their tokens fresh: builds the address users approve the app at,
/// trades the code it sends back for tokens, and refreshes an access token before it expires.
/// </summary>
public sealed class AIIntegrationTokens
{
    /// <summary>
    /// The named HTTP client integrations call out with.
    /// </summary>
    public const string HttpClientName = "AIIntegrations";

    private const string ProtectorPurpose = "CrestApps.AI.Integrations.v1";

    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAIIntegrationConnectionStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationTokens"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">Encrypts the tokens and the client secret.</param>
    /// <param name="httpClientFactory">Calls the providers.</param>
    /// <param name="store">Saves refreshed tokens.</param>
    /// <param name="clock">The clock.</param>
    public AIIntegrationTokens(IDataProtectionProvider dataProtectionProvider, IHttpClientFactory httpClientFactory, IAIIntegrationConnectionStore store, IClock clock)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _httpClientFactory = httpClientFactory;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Encrypts a secret for a connection.
    /// </summary>
    /// <param name="value">The secret.</param>
    /// <returns>The encrypted secret, or <see langword="null"/> when it is empty.</returns>
    public string Protect(string value)
        => string.IsNullOrWhiteSpace(value) ? null : _protector.Protect(value.Trim());

    /// <summary>
    /// Decrypts a connection's secret.
    /// </summary>
    /// <param name="value">The encrypted secret.</param>
    /// <returns>The secret, or <see langword="null"/>.</returns>
    public string Unprotect(string value)
        => string.IsNullOrEmpty(value) ? null : _protector.Unprotect(value);

    /// <summary>
    /// Builds the address a user approves the app at.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="redirectUri">Where the provider sends the user back.</param>
    /// <param name="state">The value that ties the answer to this request.</param>
    /// <returns>The address.</returns>
    public static string BuildAuthorizeUrl(AIIntegrationConnection connection, string redirectUri, string state)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = connection.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = AIIntegrationProviders.Scopes(connection.Provider),
            ["state"] = state,
        };

        if (connection.Provider == AIIntegrationProviders.Google)
        {
            // Without these Google issues no refresh token, and the connection stops working after an hour.
            query["access_type"] = "offline";
            query["prompt"] = "consent";
        }

        return AIIntegrationProviders.AuthorizeEndpoint(connection) + "?" + string.Join('&', query.Where(pair => !string.IsNullOrEmpty(pair.Value)).Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
    }

    /// <summary>
    /// Trades the code the provider sent back for tokens, and keeps them on the connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="code">The code.</param>
    /// <param name="redirectUri">The redirect address the code was issued for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An error, or <see langword="null"/> when the connection is signed in.</returns>
    public async Task<string> ExchangeCodeAsync(AIIntegrationConnection connection, string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return await RequestTokenAsync(connection, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        }, cancellationToken);
    }

    /// <summary>
    /// Gets a connection's access token, refreshing it when it expires in the next two minutes.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The token.</returns>
    /// <exception cref="InvalidOperationException">The connection is not signed in, or the refresh was refused.</exception>
    public async Task<string> GetAccessTokenAsync(AIIntegrationConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var token = Unprotect(connection.ProtectedAccessToken);
        var fresh = connection.AccessTokenExpiresUtc is not { } expires || expires > _clock.UtcNow.AddMinutes(2);

        if (!string.IsNullOrEmpty(token) && fresh)
        {
            return token;
        }

        if (string.IsNullOrEmpty(connection.ProtectedRefreshToken))
        {
            throw new InvalidOperationException($"The '{connection.Name}' connection is not signed in. Connect it again.");
        }

        var error = await RequestTokenAsync(connection, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = Unprotect(connection.ProtectedRefreshToken),
        }, cancellationToken);

        if (error is not null)
        {
            throw new InvalidOperationException($"The '{connection.Name}' connection could not refresh its token: {error}");
        }

        await _store.UpdateAsync(connection, cancellationToken);

        return Unprotect(connection.ProtectedAccessToken);
    }

    /// <summary>
    /// Makes an HTTP client for calling a provider with the connection's token.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The client.</returns>
    public async Task<HttpClient> CreateClientAsync(AIIntegrationConnection connection, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(connection, cancellationToken);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (connection.Provider == AIIntegrationProviders.Notion)
        {
            client.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");
        }

        return client;
    }

    private async Task<string> RequestTokenAsync(AIIntegrationConnection connection, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        form["client_id"] = connection.ClientId;
        form["client_secret"] = Unprotect(connection.ProtectedClientSecret);

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsync(AIIntegrationProviders.TokenEndpoint(connection), new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return $"{(int)response.StatusCode}: {body}";
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        connection.ProtectedAccessToken = Protect(root.TryGetProperty("access_token", out var access) ? access.GetString() : null);

        // A refresh answer usually carries no new refresh token; the old one stays good.
        if (root.TryGetProperty("refresh_token", out var refresh) && !string.IsNullOrEmpty(refresh.GetString()))
        {
            connection.ProtectedRefreshToken = Protect(refresh.GetString());
        }

        connection.AccessTokenExpiresUtc = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds)
            ? _clock.UtcNow.AddSeconds(seconds)
            : connection.Provider == AIIntegrationProviders.Salesforce ? _clock.UtcNow.AddHours(1) : null;

        if (root.TryGetProperty("instance_url", out var instance) && !string.IsNullOrEmpty(instance.GetString()))
        {
            connection.InstanceUrl = instance.GetString();
        }

        return null;
    }
}
