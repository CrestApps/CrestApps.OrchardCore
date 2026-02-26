using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using Cysharp.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

/// <summary>
/// Caches tabular batch processing results using distributed cache to avoid 
/// re-processing documents on every chat message.
/// Cache keys are based on document content hash + user prompt to ensure accurate cache hits.
/// </summary>
public sealed class TabularBatchResultCache : ITabularBatchResultCache
{
    private readonly IDistributedCache _cache;
    private readonly RowLevelTabularBatchOptions _settings;
    private readonly ILogger<TabularBatchResultCache> _logger;

    private const string CacheKeyPrefix = "tabular_batch:";
    private static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public TabularBatchResultCache(
        IDistributedCache cache,
        IOptions<RowLevelTabularBatchOptions> settings,
        ILogger<TabularBatchResultCache> logger)
    {
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string GenerateCacheKey(string interactionId, string documentContentHash, string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interactionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentContentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        // Create a composite key from interaction ID, document hash, and prompt hash
        var promptHash = ComputeHash(prompt);
        return $"{CacheKeyPrefix}{interactionId}:{documentContentHash}:{promptHash}";
    }

    /// <inheritdoc />
    public string ComputeDocumentContentHash(IEnumerable<(string FileName, string Content)> documents)
    {
        if (documents is null)
        {
            return string.Empty;
        }
        var builder = ZString.CreateStringBuilder();

        foreach (var (fileName, content) in documents.OrderBy(d => d.FileName))
        {
            builder.Append(fileName ?? "unknown");
            builder.Append(':');
            builder.Append(content ?? string.Empty);
            builder.Append('|');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    /// <inheritdoc />
    public TabularBatchCacheEntry TryGet(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return null;
        }

        try
        {
            var cachedBytes = _cache.Get(cacheKey);

            if (cachedBytes is null || cachedBytes.Length == 0)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Cache miss for tabular batch results. Key: {CacheKey}", cacheKey);
                }
                return null;
            }

            var entry = JsonSerializer.Deserialize<TabularBatchCacheEntry>(cachedBytes, _jsonOptions);

            if (entry is not null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Cache hit for tabular batch results. Key: {CacheKey}", cacheKey);
                }
            }

            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving cached batch results. Key: {CacheKey}", cacheKey);
            return null;
        }
    }

    /// <inheritdoc />
    public void Set(string cacheKey, TabularBatchCacheEntry entry, TimeSpan? expiration = null)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || entry is null)
        {
            return;
        }

        try
        {
            var cacheExpiration = expiration ?? GetCacheExpiration();

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheExpiration,
                SlidingExpiration = TimeSpan.FromMinutes(10),
            };

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(entry, _jsonOptions);
            _cache.Set(cacheKey, jsonBytes, options);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Cached tabular batch results. Key: {CacheKey}, Batches: {BatchCount}, Size: {Size} bytes, Expiration: {Expiration}",
                    cacheKey, entry.Results?.Count ?? 0, jsonBytes.Length, cacheExpiration);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching batch results. Key: {CacheKey}", cacheKey);
        }
    }

    /// <inheritdoc />
    public void Remove(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }

        try
        {
            _cache.Remove(cacheKey);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Removed cached tabular batch results. Key: {CacheKey}", cacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing cached batch results. Key: {CacheKey}", cacheKey);
        }
    }

    /// <inheritdoc />
    public void InvalidateForInteraction(string interactionId)
    {
        // Distributed cache doesn't support prefix-based removal directly.
        // This is a limitation - entries will expire naturally.
        // For production, consider using Redis with SCAN/DEL or a custom key tracking mechanism.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Invalidation requested for interaction {InteractionId}. " +
                "Note: Distributed cache does not support prefix-based removal. " +
                "Entries will expire naturally based on configured TTL.",
                interactionId);
        }
    }

    private TimeSpan GetCacheExpiration()
    {
        var minutes = _settings.CacheExpirationMinutes;
        return minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : DefaultCacheExpiration;
    }

    private static string ComputeHash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes)[..16]; // Truncate for shorter keys
    }
}
