using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// Adds a connection's credentials to a request: an API key, Basic credentials, a bearer token, or a token from an
/// OAuth 2.0 token endpoint, found through an OpenID Connect discovery document when the connection names an issuer.
/// </summary>
/// <remarks>
/// Tokens are cached per tenant and connection until a minute before they expire; a request answered with 401 clears
/// the cached token so the next one asks for a new token. Token endpoints and discovery documents must use HTTPS.
/// </remarks>
internal sealed class HttpRequestAuthenticator
{
    private static readonly TimeSpan _discoveryCacheDuration = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly string _tenant;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public HttpRequestAuthenticator(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ShellSettings shellSettings)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _tenant = shellSettings?.Name ?? string.Empty;
    }

    /// <summary>
    /// Returns whether a kind of authentication uses tokens that can expire and be asked for again.
    /// </summary>
    public static bool UsesTokens(HttpAuthenticationSettings settings)
        => settings?.Type is HttpAuthenticationType.OAuth2 or HttpAuthenticationType.OpenIdConnect;

    /// <summary>
    /// Adds the credentials to a request.
    /// </summary>
    public async Task ApplyAsync(
        HttpRequestMessage request,
        HttpAuthenticationSettings settings,
        IDataConnectorSecrets secrets,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(secrets);

        switch (settings?.Type ?? HttpAuthenticationType.None)
        {
            case HttpAuthenticationType.ApiKey:
                ApplyApiKey(request, settings, Required(secrets, HttpAuthenticationSecrets.ApiKey, "API key"));
                break;

            case HttpAuthenticationType.Basic:
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{secrets.Get(HttpAuthenticationSecrets.Password)}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;

            case HttpAuthenticationType.Bearer:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Required(secrets, HttpAuthenticationSecrets.BearerToken, "bearer token"));
                break;

            case HttpAuthenticationType.OAuth2:
            case HttpAuthenticationType.OpenIdConnect:
                var token = await GetTokenAsync(settings, secrets, cacheKey, cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                break;
        }
    }

    /// <summary>
    /// Forgets the cached token of a connection.
    /// </summary>
    public void Invalidate(string cacheKey)
        => _cache.Remove(TokenKey(cacheKey));

    /// <summary>
    /// Asks the token endpoint for a token, without the cache, to check the settings.
    /// </summary>
    public async Task<string> RequestTokenAsync(HttpAuthenticationSettings settings, IDataConnectorSecrets secrets, CancellationToken cancellationToken)
    {
        var (token, _) = await RequestTokenCoreAsync(settings, secrets, cancellationToken);

        return token;
    }

    private async Task<string> GetTokenAsync(HttpAuthenticationSettings settings, IDataConnectorSecrets secrets, string cacheKey, CancellationToken cancellationToken)
    {
        var key = TokenKey(cacheKey);

        if (_cache.TryGetValue(key, out string cached))
        {
            return cached;
        }

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            if (_cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var (token, expiresIn) = await RequestTokenCoreAsync(settings, secrets, cancellationToken);
            var lifetime = TimeSpan.FromSeconds(Math.Max(30, expiresIn - 60));

            _cache.Set(key, token, lifetime);

            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(string Token, int ExpiresIn)> RequestTokenCoreAsync(HttpAuthenticationSettings settings, IDataConnectorSecrets secrets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            throw new DataConnectorException("The connection has no client identifier.");
        }

        var endpoint = settings.Type == HttpAuthenticationType.OpenIdConnect
            ? await GetTokenEndpointAsync(settings.Authority, cancellationToken)
            : RequireHttps(settings.TokenUrl, "token endpoint");

        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", settings.GrantType == OAuthGrantType.Password ? "password" : "client_credentials"),
        };

        if (settings.GrantType == OAuthGrantType.Password)
        {
            form.Add(new("username", settings.UserName ?? string.Empty));
            form.Add(new("password", secrets.Get(HttpAuthenticationSecrets.Password) ?? string.Empty));
        }

        if (!string.IsNullOrWhiteSpace(settings.Scope))
        {
            form.Add(new("scope", settings.Scope.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(settings.Audience))
        {
            form.Add(new("audience", settings.Audience.Trim()));
        }

        var clientSecret = secrets.Get(HttpAuthenticationSecrets.ClientSecret) ?? string.Empty;

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (settings.ClientAuthentication == OAuthClientAuthentication.RequestBody)
        {
            form.Add(new("client_id", settings.ClientId));
            form.Add(new("client_secret", clientSecret));
        }
        else
        {
            // RFC 6749 section 2.3.1: the identifier and secret are form-encoded before they are joined.
            var basic = $"{Uri.EscapeDataString(settings.ClientId)}:{Uri.EscapeDataString(clientSecret)}";
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(basic)));
        }

        request.Content = new FormUrlEncodedContent(form);

        using var client = _httpClientFactory.CreateClient(DataConnectorConstants.HttpClientName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        HttpResponseMessage response;

        try
        {
            response = await client.SendAsync(request, timeout.Token);
        }
        catch (HttpRequestException exception)
        {
            throw new DataConnectorException($"Couldn't reach the token endpoint {endpoint.GetLeftPart(UriPartial.Path)}: {exception.Message}", exception) { IsTransient = true };
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DataConnectorException($"The token endpoint {endpoint.GetLeftPart(UriPartial.Path)} didn't answer in time.", exception) { IsTransient = true };
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new DataConnectorException($"The token endpoint refused the credentials ({(int)response.StatusCode}{TokenError(body)}).");
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                if (!root.TryGetProperty("access_token", out var accessToken) || accessToken.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(accessToken.GetString()))
                {
                    throw new DataConnectorException("The token endpoint answered without an access token.");
                }

                var expiresIn = root.TryGetProperty("expires_in", out var expires) && expires.ValueKind == JsonValueKind.Number && expires.TryGetInt32(out var seconds)
                    ? seconds
                    : 3600;

                return (accessToken.GetString(), expiresIn);
            }
            catch (JsonException exception)
            {
                throw new DataConnectorException("The token endpoint didn't answer with JSON.", exception);
            }
        }
    }

    private async Task<Uri> GetTokenEndpointAsync(string authority, CancellationToken cancellationToken)
    {
        var issuer = RequireHttps(authority, "OpenID Connect issuer");
        var discovery = issuer.AbsolutePath.Contains("/.well-known/", StringComparison.OrdinalIgnoreCase)
            ? issuer
            : new Uri(issuer.AbsoluteUri.TrimEnd('/') + "/.well-known/openid-configuration");
        var key = $"CrestApps.DataConnectors.OpenId:{_tenant}:{discovery.AbsoluteUri}";

        if (_cache.TryGetValue(key, out Uri cached))
        {
            return cached;
        }

        using var client = _httpClientFactory.CreateClient(DataConnectorConstants.HttpClientName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        string body;

        try
        {
            using var response = await client.GetAsync(discovery, timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw new DataConnectorException($"The OpenID Connect discovery document {discovery} answered {(int)response.StatusCode}.");
            }

            body = await response.Content.ReadAsStringAsync(timeout.Token);
        }
        catch (HttpRequestException exception)
        {
            throw new DataConnectorException($"Couldn't read the OpenID Connect discovery document {discovery}: {exception.Message}", exception) { IsTransient = true };
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DataConnectorException($"The OpenID Connect discovery document {discovery} didn't answer in time.", exception) { IsTransient = true };
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("token_endpoint", out var tokenEndpoint) || tokenEndpoint.ValueKind != JsonValueKind.String)
            {
                throw new DataConnectorException($"The OpenID Connect discovery document {discovery} names no token endpoint.");
            }

            var endpoint = RequireHttps(tokenEndpoint.GetString(), "token endpoint");

            _cache.Set(key, endpoint, _discoveryCacheDuration);

            return endpoint;
        }
        catch (JsonException exception)
        {
            throw new DataConnectorException($"The OpenID Connect discovery document {discovery} isn't JSON.", exception);
        }
    }

    private static void ApplyApiKey(HttpRequestMessage request, HttpAuthenticationSettings settings, string apiKey)
    {
        var name = string.IsNullOrWhiteSpace(settings.ApiKeyName) ? "X-API-Key" : settings.ApiKeyName.Trim();

        if (settings.ApiKeyLocation == HttpApiKeyLocation.Query)
        {
            var builder = new UriBuilder(request.RequestUri);
            var query = builder.Query.TrimStart('?');
            var parameter = $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(apiKey)}";

            builder.Query = string.IsNullOrEmpty(query) ? parameter : $"{query}&{parameter}";
            request.RequestUri = builder.Uri;

            return;
        }

        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, (settings.ApiKeyPrefix ?? string.Empty) + apiKey);
    }

    private static string Required(IDataConnectorSecrets secrets, string name, string label)
    {
        var value = secrets.Get(name);

        if (string.IsNullOrEmpty(value))
        {
            throw new DataConnectorException($"The connection has no {label}. Enter it again in the connection's settings.");
        }

        return value;
    }

    private static Uri RequireHttps(string value, string label)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new DataConnectorException($"The {label} must be an absolute https:// URL.");
        }

        return uri;
    }

    private static string TokenError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                return ": " + error.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private string TokenKey(string cacheKey)
        => $"CrestApps.DataConnectors.Token:{_tenant}:{cacheKey}";
}
