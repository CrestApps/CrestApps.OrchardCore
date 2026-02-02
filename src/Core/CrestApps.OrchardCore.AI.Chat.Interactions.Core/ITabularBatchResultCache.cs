using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Interface for caching tabular batch processing results to avoid re-processing
/// documents on every chat message.
/// </summary>
public interface ITabularBatchResultCache
{
    /// <summary>
    /// Generates a cache key based on interaction ID, document content hash, and prompt.
    /// </summary>
    /// <param name="interactionId">The chat interaction ID.</param>
    /// <param name="documentContentHash">Hash of the document contents.</param>
    /// <param name="prompt">The user's prompt.</param>
    /// <returns>A unique cache key.</returns>
    string GenerateCacheKey(string interactionId, string documentContentHash, string prompt);

    /// <summary>
    /// Computes a hash of the document contents for cache key generation.
    /// </summary>
    /// <param name="documents">The documents to hash.</param>
    /// <returns>A hash string representing the document contents.</returns>
    string ComputeDocumentContentHash(IEnumerable<(string FileName, string Content)> documents);

    /// <summary>
    /// Attempts to retrieve cached batch results.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <returns>The cached entry if found; otherwise, null.</returns>
    TabularBatchCacheEntry TryGet(string cacheKey);

    /// <summary>
    /// Stores batch results in the cache.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="entry">The cache entry to store.</param>
    /// <param name="expiration">Optional custom expiration time.</param>
    void Set(string cacheKey, TabularBatchCacheEntry entry, TimeSpan? expiration = null);

    /// <summary>
    /// Removes a specific cache entry.
    /// </summary>
    /// <param name="cacheKey">The cache key to remove.</param>
    void Remove(string cacheKey);

    /// <summary>
    /// Invalidates all cached results for a specific interaction.
    /// Called when documents are added/removed from an interaction.
    /// </summary>
    /// <param name="interactionId">The interaction ID to invalidate.</param>
    void InvalidateForInteraction(string interactionId);
}
