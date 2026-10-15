using System.Collections.Concurrent;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IVoiceMediaAvailabilityCache"/>: an in-memory, per-tenant memory of the provider copies a
/// provider has confirmed it holds, each remembered for <see cref="Window"/>.
/// </summary>
public sealed class VoiceMediaAvailabilityCache : IVoiceMediaAvailabilityCache
{
    /// <summary>
    /// How long a provider's confirmation that it holds a copy is trusted before the provider is asked again.
    /// </summary>
    /// <remarks>
    /// The window trades one provider round trip per play for how soon a copy lost at the provider is noticed. Inside
    /// it a clip plays with no request to the provider beyond the play itself; a copy deleted or expired at the
    /// provider in the meantime is noticed, and made again from the master copy, at the first play after the window
    /// ends rather than at the very next play. Ten minutes keeps a busy queue from asking after its hold music on
    /// every call while bounding the silence a lost copy can cause to that many minutes.
    /// </remarks>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    // Expired entries are removed when they are read or when the memory grows past this, so a tenant with many clips
    // and a long-running node is bounded without a timer.
    private const int SweepThreshold = 4096;

    private readonly ConcurrentDictionary<string, DateTime> _confirmedUntilUtc = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaAvailabilityCache"/> class.
    /// </summary>
    /// <param name="clock">The clock the window is measured by.</param>
    public VoiceMediaAvailabilityCache(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public bool IsKnownAvailable(string providerName, string mediaReference)
    {
        if (string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(mediaReference))
        {
            return false;
        }

        var key = GetKey(providerName, mediaReference);

        if (!_confirmedUntilUtc.TryGetValue(key, out var confirmedUntilUtc))
        {
            return false;
        }

        if (confirmedUntilUtc > _clock.UtcNow)
        {
            return true;
        }

        _confirmedUntilUtc.TryRemove(key, out _);

        return false;
    }

    /// <inheritdoc/>
    public void MarkAvailable(string providerName, string mediaReference)
    {
        if (string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(mediaReference))
        {
            return;
        }

        var now = _clock.UtcNow;

        if (_confirmedUntilUtc.Count >= SweepThreshold)
        {
            foreach (var entry in _confirmedUntilUtc.Where(entry => entry.Value <= now).ToArray())
            {
                _confirmedUntilUtc.TryRemove(entry.Key, out _);
            }
        }

        _confirmedUntilUtc[GetKey(providerName, mediaReference)] = now.Add(Window);
    }

    /// <inheritdoc/>
    public void Forget(string providerName, string mediaReference)
    {
        if (string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(mediaReference))
        {
            return;
        }

        _confirmedUntilUtc.TryRemove(GetKey(providerName, mediaReference), out _);
    }

    // Provider technical names are matched without regard to case everywhere a copy is looked up, so they are here too.
    private static string GetKey(string providerName, string mediaReference)
        => string.Concat(providerName.ToUpperInvariant(), "\n", mediaReference);
}
