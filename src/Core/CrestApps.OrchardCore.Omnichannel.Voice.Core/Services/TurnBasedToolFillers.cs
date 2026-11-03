using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The short lines ("one moment") a turn-based call spoke while a tool was running, whose end of speech must not be
/// taken for the end of the AI's reply.
/// </summary>
/// <remarks>
/// When a line ends, the loop starts listening to the caller again. A filler line ends while the model is still
/// waiting for its tool, so its end is skipped: the reply that follows ends the turn. Kept in memory per provider call
/// identifier, which is unique across tenants, like the caller's keys; an entry nobody claims expires.
/// </remarks>
internal static class TurnBasedToolFillers
{
    private static readonly TimeSpan _expiry = TimeSpan.FromMinutes(2);
    private static readonly ConcurrentDictionary<string, Pending> _pending = new(StringComparer.Ordinal);

    /// <summary>
    /// Records that a filler line was spoken on a call.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier of the call leg.</param>
    /// <param name="nowUtc">The current time.</param>
    public static void Spoke(string providerCallId, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(providerCallId))
        {
            return;
        }

        _pending.AddOrUpdate(providerCallId, _ => new Pending(1, nowUtc), (_, current) => new Pending(current.Count + 1, nowUtc));

        foreach (var entry in _pending)
        {
            if (nowUtc - entry.Value.SpokenUtc > _expiry)
            {
                _pending.TryRemove(entry);
            }
        }
    }

    /// <summary>
    /// Claims the end of one filler line on a call.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier of the call leg.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the line that ended was a filler, so the turn is not over.</returns>
    public static bool TryClaim(string providerCallId, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(providerCallId))
        {
            return false;
        }

        while (_pending.TryGetValue(providerCallId, out var current))
        {
            if (nowUtc - current.SpokenUtc > _expiry)
            {
                _pending.TryRemove(new KeyValuePair<string, Pending>(providerCallId, current));

                return false;
            }

            var claimed = current.Count <= 1
                ? _pending.TryRemove(new KeyValuePair<string, Pending>(providerCallId, current))
                : _pending.TryUpdate(providerCallId, current with { Count = current.Count - 1 }, current);

            if (claimed)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Forgets every pending filler. For tests.
    /// </summary>
    internal static void Clear()
        => _pending.Clear();

    private sealed record Pending(int Count, DateTime SpokenUtc);
}
