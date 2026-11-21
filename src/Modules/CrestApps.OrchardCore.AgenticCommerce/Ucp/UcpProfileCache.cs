using System.Text.Json.Nodes;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Keeps the UCP profiles the store read, for as long as their cache headers allow, within a fixed number of entries
/// and bytes: a caller naming ever new profile addresses can't grow it without end. When it is full, expired entries
/// go first, then those that expire soonest.
/// </summary>
public sealed class UcpProfileCache
{
    /// <summary>
    /// The most profiles kept.
    /// </summary>
    public const int MaxEntries = 1000;

    /// <summary>
    /// The most bytes of profiles kept.
    /// </summary>
    public const long MaxBytes = 16L * 1024 * 1024;

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private readonly IClock _clock;

    private long _bytes;
    private long _sequence;

    /// <summary>
    /// Initializes a new instance of the <see cref="UcpProfileCache"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public UcpProfileCache(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Returns a kept profile that hasn't expired, or <see langword="null"/>.
    /// </summary>
    /// <param name="url">The profile's address.</param>
    public JsonObject Get(string url)
        => TryGet(url, out var profile, out _) ? profile : null;

    /// <summary>
    /// Returns whether a profile, or why it couldn't be read, is remembered for an address.
    /// </summary>
    /// <param name="url">The profile's address.</param>
    /// <param name="profile">The profile, or <see langword="null"/> when it couldn't be read.</param>
    /// <param name="error">Why the profile couldn't be read, or <see langword="null"/>.</param>
    public bool TryGet(string url, out JsonObject profile, out string error)
    {
        profile = null;
        error = null;

        lock (_lock)
        {
            if (!_entries.TryGetValue(url, out var entry))
            {
                return false;
            }

            if (entry.ExpiresUtc <= _clock.UtcNow)
            {
                Remove(url, entry);

                return false;
            }

            profile = entry.Profile;
            error = entry.Error;

            return true;
        }
    }

    /// <summary>
    /// Remembers for a while that a profile couldn't be read, so a caller can't make the store fetch it on every
    /// request.
    /// </summary>
    /// <param name="url">The profile's address.</param>
    /// <param name="error">Why it couldn't be read.</param>
    /// <param name="lifetime">How long to remember it.</param>
    public void SetFailure(string url, string error, TimeSpan lifetime)
        => Set(url, null, error, size: 1, lifetime);

    /// <summary>
    /// Keeps a profile until <paramref name="lifetime"/> passes, making room by dropping others when the cache is full.
    /// </summary>
    /// <param name="url">The profile's address.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="size">The profile's size in bytes, as read.</param>
    /// <param name="lifetime">How long to keep it.</param>
    public void Set(string url, JsonObject profile, long size, TimeSpan lifetime)
        => Set(url, profile, error: null, size, lifetime);

    private void Set(string url, JsonObject profile, string error, long size, TimeSpan lifetime)
    {
        if (size > MaxBytes)
        {
            return;
        }

        lock (_lock)
        {
            var now = _clock.UtcNow;

            if (_entries.TryGetValue(url, out var existing))
            {
                Remove(url, existing);
            }

            while (_entries.Count >= MaxEntries || _bytes + size > MaxBytes)
            {
                var victim = _entries
                    .OrderBy(pair => pair.Value.ExpiresUtc <= now ? 0 : 1)
                    .ThenBy(pair => pair.Value.ExpiresUtc)
                    .ThenBy(pair => pair.Value.Sequence)
                    .First();

                Remove(victim.Key, victim.Value);
            }

            _entries[url] = new Entry(profile, error, size, now.Add(lifetime), _sequence++);
            _bytes += size;
        }
    }

    private void Remove(string url, Entry entry)
    {
        _entries.Remove(url);
        _bytes -= entry.Size;
    }

    private sealed record Entry(JsonObject Profile, string Error, long Size, DateTime ExpiresUtc, long Sequence);
}
