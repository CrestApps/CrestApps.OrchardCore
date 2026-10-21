using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Builds a feed once in 15 minutes for each currency, and checks a feed's key.
/// </summary>
public sealed class ProductFeedService
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(15);

    private readonly ProductFeedBuilder _builder;
    private readonly IMemoryCache _cache;
    private readonly ShellSettings _shellSettings;
    private readonly ISiteService _siteService;

    public ProductFeedService(
        ProductFeedBuilder builder,
        IMemoryCache cache,
        ShellSettings shellSettings,
        ISiteService siteService)
    {
        _builder = builder;
        _cache = cache;
        _shellSettings = shellSettings;
        _siteService = siteService;
    }

    public async Task<ProductFeed> GetAsync(string currency, bool fresh = false, CancellationToken cancellationToken = default)
    {
        var key = $"ProductFeed:{_shellSettings.Name}:{currency?.ToUpperInvariant()}";

        if (!fresh && _cache.TryGetValue(key, out ProductFeed cached))
        {
            return cached;
        }

        var feed = await _builder.BuildAsync(currency, cancellationToken);

        _cache.Set(key, feed, _lifetime);

        return feed;
    }

    /// <summary>
    /// Returns whether a request may read the feed: it's public, or the request carries its key.
    /// </summary>
    public async Task<bool> IsAllowedAsync(string key)
    {
        var settings = await _siteService.GetSettingsAsync<ProductFeedSettings>();

        return string.IsNullOrEmpty(settings.Key) ||
            (!string.IsNullOrEmpty(key) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(settings.Key)));
    }
}
