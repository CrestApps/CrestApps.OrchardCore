using System.Collections.Concurrent;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Keeps each user's count on the Messaging admin menu item for a few seconds. Every admin page asks for the count
/// after it loads, and each answer is two counts over the conversation table, so a user moving from page to page
/// would otherwise query the database on every click. It is registered as a per-tenant singleton, so the counts it
/// holds belong to one tenant, and each instance of the application keeps its own.
/// </summary>
public sealed class MessagingAttentionCountCache
{
    /// <summary>
    /// How long a user's count is answered from memory before it is read again.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(20);

    // Past this many users the expired entries are dropped when a count is kept, so users who signed out long ago are
    // not held for the life of the tenant.
    private const int SweepThreshold = 512;

    private readonly IClock _clock;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingAttentionCountCache"/> class.
    /// </summary>
    /// <param name="clock">The clock the lifetime is measured with.</param>
    public MessagingAttentionCountCache(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Returns the user's kept count while it is within its lifetime, and otherwise reads and keeps a new one.
    /// </summary>
    /// <param name="userId">The user the count belongs to; a user without an identifier is never kept.</param>
    /// <param name="factory">Reads the count from the database.</param>
    /// <param name="refresh">Whether to read the count even when a kept one could be used, as after a notification
    /// says it changed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    public async Task<int> GetOrReadAsync(
        string userId,
        Func<CancellationToken, Task<int>> factory,
        bool refresh,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (string.IsNullOrEmpty(userId))
        {
            return await factory(cancellationToken);
        }

        var now = _clock.UtcNow;

        if (!refresh && _entries.TryGetValue(userId, out var entry) && now < entry.ExpiresUtc)
        {
            return entry.Count;
        }

        var count = await factory(cancellationToken);

        if (_entries.Count >= SweepThreshold)
        {
            foreach (var pair in _entries)
            {
                if (pair.Value.ExpiresUtc <= now)
                {
                    _entries.TryRemove(pair);
                }
            }
        }

        _entries[userId] = new Entry(count, now.Add(Lifetime));

        return count;
    }

    /// <summary>
    /// Forgets the user's kept count, so the next request reads it, as after the user read a conversation.
    /// </summary>
    /// <param name="userId">The user.</param>
    public void Remove(string userId)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            _entries.TryRemove(userId, out _);
        }
    }

    private sealed record Entry(int Count, DateTime ExpiresUtc);
}
