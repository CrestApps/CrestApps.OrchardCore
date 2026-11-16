using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OrchardCore.Modules;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Finds tools in the external tool store and their clients in Orchard Core's OpenID applications. A tool's JWKS URL is
/// read with a 10-second timeout and at most 256 KB, cached for 10 minutes, and read again at most once a minute.
/// </summary>
public sealed class LtiToolDirectory : ILtiToolDirectory
{
    /// <summary>
    /// The name of the HTTP client that reads tools' key sets.
    /// </summary>
    public const string HttpClientName = "CrestApps.Learning.Lti.Jwks";

    private const int MaxJwksBytes = 256 * 1024;

    private static readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(1);

    private readonly IExternalToolStore _store;
    private readonly IOpenIdApplicationManager _applications;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiToolDirectory"/> class.
    /// </summary>
    /// <param name="store">The external tool store.</param>
    /// <param name="applications">The OpenID application manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public LtiToolDirectory(
        IExternalToolStore store,
        IOpenIdApplicationManager applications,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IClock clock,
        ILogger<LtiToolDirectory> logger)
    {
        _store = store;
        _applications = applications;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExternalTool> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        var tool = await _store.FindByClientIdAsync(clientId, cancellationToken);

        if (tool is null || await _applications.FindByClientIdAsync(clientId, cancellationToken) is null)
        {
            return null;
        }

        return tool;
    }

    /// <inheritdoc />
    public async Task<ExternalTool> FindByIdAsync(string toolId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(toolId) ? null : await _store.FindByIdAsync(toolId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsRedirectUriAllowedAsync(string clientId, string redirectUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(redirectUri) || !Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
        {
            return false;
        }

        var application = await _applications.FindByClientIdAsync(clientId, cancellationToken);

        return application is not null && await _applications.ValidateRedirectUriAsync(application, redirectUri, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(ExternalTool tool, bool refresh = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (!string.IsNullOrWhiteSpace(tool.JwksUrl))
        {
            return await ReadJwksUrlAsync(tool.JwksUrl, refresh, cancellationToken);
        }

        var application = await _applications.FindByClientIdAsync(tool.ClientId, cancellationToken);
        var keySet = application is null ? null : await _applications.GetJsonWebKeySetAsync(application, cancellationToken);

        return keySet?.GetSigningKeys().ToArray() ?? [];
    }

    private async Task<IReadOnlyList<SecurityKey>> ReadJwksUrlAsync(string url, bool refresh, CancellationToken cancellationToken)
    {
        var cacheKey = "CrestApps.Lti.Jwks:" + url;

        if (_cache.TryGetValue(cacheKey, out CachedKeys cached) && (!refresh || _clock.UtcNow - cached.ReadUtc < _refreshInterval))
        {
            return cached.Keys;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > MaxJwksBytes)
            {
                throw new InvalidOperationException("The key set is too large.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var buffer = new char[MaxJwksBytes + 1];
            var read = await reader.ReadBlockAsync(buffer, cancellationToken);

            if (read > MaxJwksBytes)
            {
                throw new InvalidOperationException("The key set is too large.");
            }

            var keys = new JsonWebKeySet(new string(buffer, 0, read)).GetSigningKeys().ToArray();

            _cache.Set(cacheKey, new CachedKeys(keys, _clock.UtcNow), _cacheLifetime);

            return keys;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(exception, "The key set of an external tool could not be read from its JWKS URL.");

            return cached?.Keys ?? [];
        }
    }

    private sealed record CachedKeys(IReadOnlyList<SecurityKey> Keys, DateTime ReadUtc);
}
