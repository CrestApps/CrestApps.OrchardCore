using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Products.Core.Feeds;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Environment.Cache;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Builds a feed once in 15 minutes for each currency, or again after the <see cref="ProductFeedCache.SignalKey"/>
/// signal (such as a stock change), and checks a feed's key.
/// </summary>
public sealed class ProductFeedService : IProductFeedService
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(15);

    private readonly ProductFeedBuilder _builder;
    private readonly IMemoryCache _cache;
    private readonly ISignal _signal;
    private readonly ShellSettings _shellSettings;
    private readonly ISiteService _siteService;

    public ProductFeedService(
        ProductFeedBuilder builder,
        IMemoryCache cache,
        ISignal signal,
        ShellSettings shellSettings,
        ISiteService siteService)
    {
        _builder = builder;
        _cache = cache;
        _signal = signal;
        _shellSettings = shellSettings;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async Task<ProductFeed> GetAsync(string currency, bool fresh = false, CancellationToken cancellationToken = default)
    {
        var key = $"ProductFeed:{_shellSettings.Name}:{currency?.ToUpperInvariant()}";

        if (!fresh && _cache.TryGetValue(key, out ProductFeed cached))
        {
            return cached;
        }

        // The token is taken before the build, so a stock change during it still drops the copy.
        var token = _signal.GetToken(ProductFeedCache.SignalKey);
        var feed = await _builder.BuildAsync(currency, contentItemIds: null, cancellationToken);

        _cache.Set(key, feed, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(_lifetime)
            .AddExpirationToken(token));

        return feed;
    }

    /// <inheritdoc/>
    public Task<ProductFeed> BuildAsync(IEnumerable<string> contentItemIds, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentItemIds);

        return _builder.BuildAsync(currency, contentItemIds.ToArray(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsAllowedAsync(string key)
    {
        var settings = await _siteService.GetSettingsAsync<ProductFeedSettings>();

        return string.IsNullOrEmpty(settings.Key) ||
            (!string.IsNullOrEmpty(key) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(settings.Key)));
    }

    /// <inheritdoc/>
    public async Task<string> GetKeyAsync()
    {
        var key = (await _siteService.GetSettingsAsync<ProductFeedSettings>()).Key;

        return string.IsNullOrEmpty(key) ? null : key;
    }
}
