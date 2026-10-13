using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Listing;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Environment.Cache;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Keeps recent search results in memory, so the same listing page and filters are counted once rather than on
/// every visit.
/// </summary>
/// <remarks>
/// Results are cached for a few minutes per tenant and per query. Any change to the search records signals
/// <see cref="SignalKey"/> once the change is committed, which drops every cached result of the tenant, so a
/// published, unpublished or re-indexed product shows on the next request. On a web farm the signal is local to
/// a node, so another node can show a result up to <see cref="Duration"/> old.
/// </remarks>
public sealed class CachedProductSearchService : IProductSearchService
{
    /// <summary>
    /// The signal raised when the search records change.
    /// </summary>
    public const string SignalKey = "CrestApps.ProductSearchIndex";

    /// <summary>
    /// How long a result is kept.
    /// </summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);

    private readonly SqlProductSearchService _inner;
    private readonly IMemoryCache _memoryCache;
    private readonly ISignal _signal;
    private readonly ShellSettings _shellSettings;

    public CachedProductSearchService(
        SqlProductSearchService inner,
        IMemoryCache memoryCache,
        ISignal signal,
        ShellSettings shellSettings)
    {
        _inner = inner;
        _memoryCache = memoryCache;
        _signal = signal;
        _shellSettings = shellSettings;
    }

    public async Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The memory cache is shared by every tenant of the host, so the key carries the tenant.
        var key = $"{SignalKey}:{_shellSettings.Name}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(query))))}";

        if (_memoryCache.TryGetValue(key, out ProductSearchResult cached))
        {
            return cached;
        }

        var result = await _inner.SearchAsync(query, cancellationToken);

        _memoryCache.Set(key, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Duration,
            Size = 1,
        }.AddExpirationToken(_signal.GetToken(SignalKey)));

        return result;
    }
}
