using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Fetches agent platforms' UCP profiles, through the egress guard (no hosts on the site's own network, no redirects),
/// and keeps them as long as their cache headers allow.
/// </summary>
public sealed class UcpProfileClient
{
    /// <summary>
    /// The name of the HTTP client that fetches profiles.
    /// </summary>
    public const string HttpClientName = "AgenticCommerce.Profiles";

    private const int MaxProfileLength = 256 * 1024;

    private static readonly TimeSpan _minimumLifetime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan _maximumLifetime = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UcpProfileClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="logger">The logger.</param>
    public UcpProfileClient(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<UcpProfileClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Returns a platform's profile, or why it can't be read: <c>invalid_profile_url</c>, <c>profile_unreachable</c> or
    /// <c>profile_malformed</c>.
    /// </summary>
    /// <param name="profileUrl">The profile's address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<(JsonObject Profile, string Error)> GetAsync(string profileUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(profileUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return (null, "invalid_profile_url");
        }

        var cacheKey = "ucp-profile:" + uri.AbsoluteUri;

        if (_cache.TryGetValue(cacheKey, out JsonObject cached))
        {
            return (cached, null);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return (null, "profile_unreachable");
            }

            if (response.Content.Headers.ContentLength > MaxProfileLength)
            {
                return (null, "profile_malformed");
            }

            var body = await response.Content.ReadAsByteArrayAsync(timeout.Token);

            if (body.Length > MaxProfileLength || JsonNode.Parse(body) is not JsonObject profile || profile["ucp"] is not JsonObject)
            {
                return (null, "profile_malformed");
            }

            var lifetime = response.Headers.CacheControl?.MaxAge ?? _minimumLifetime;

            _cache.Set(cacheKey, profile, lifetime < _minimumLifetime ? _minimumLifetime : lifetime > _maximumLifetime ? _maximumLifetime : lifetime);

            return (profile, null);
        }
        catch (JsonException)
        {
            return (null, "profile_malformed");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "The UCP profile at '{Host}' couldn't be read.", uri.Host);

            return (null, "profile_unreachable");
        }
    }

    /// <summary>
    /// Returns where a platform's profile says its order updates go, or <see langword="null"/>.
    /// </summary>
    /// <param name="profile">The profile.</param>
    public static string GetOrderWebhookUrl(JsonObject profile)
    {
        if (profile?["ucp"]?["capabilities"]?[UcpConstants.Capabilities.Order] is not JsonArray entries)
        {
            return null;
        }

        foreach (var entry in entries)
        {
            if (entry?["config"]?["webhook_url"]?.GetValueKind() == JsonValueKind.String)
            {
                return entry["config"]["webhook_url"].GetValue<string>();
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the made-up id an unregistered platform's checkouts are kept under, from its profile's address.
    /// </summary>
    /// <param name="profileUrl">The profile's address.</param>
    public static string GetUnregisteredPlatformId(string profileUrl)
        => $"ucp-{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(profileUrl ?? string.Empty)).AsSpan(0, 11))}";

    /// <summary>
    /// The storage key of where a UCP platform's order updates go, on an agent checkout.
    /// </summary>
    public const string WebhookUrlProperty = "ucp.webhook_url";

    /// <summary>
    /// The storage key of a UCP platform's profile address, on an agent checkout.
    /// </summary>
    public const string ProfileUrlProperty = "ucp.profile_url";
}
