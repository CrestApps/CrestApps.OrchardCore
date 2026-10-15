using System.Collections.Concurrent;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IVoiceMediaUploadCoordinator"/>: an in-process, per-tenant lock per clip and provider, and a
/// short memory of the reference each upload produced, so callers that arrive while a copy is being made, or while it
/// is still being recorded on its record, reuse it.
/// </summary>
public sealed class VoiceMediaUploadCoordinator : IVoiceMediaUploadCoordinator
{
    /// <summary>
    /// How long the reference of a completed upload is reused for the same clip and provider.
    /// </summary>
    /// <remarks>
    /// A copy is recorded on its record in a deferred task after the call that made it commits, so a call that
    /// arrives in between still finds no copy recorded. Reusing the reference for a minute covers that gap without
    /// keeping a stale answer: a copy made more than a minute ago is either recorded by then or was not meant to be.
    /// </remarks>
    public static readonly TimeSpan RecentUploadWindow = TimeSpan.FromMinutes(1);

    private const int SweepThreshold = 1024;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (string Reference, DateTime ExpiresUtc)> _recent = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaUploadCoordinator"/> class.
    /// </summary>
    /// <param name="clock">The clock the reuse window is measured by.</param>
    public VoiceMediaUploadCoordinator(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<string> UploadOnceAsync(string providerName, string masterStorageReference, Func<CancellationToken, Task<string>> upload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(masterStorageReference);
        ArgumentNullException.ThrowIfNull(upload);

        var key = string.Concat(providerName.ToUpperInvariant(), "\n", masterStorageReference);

        if (TryGetRecent(key, out var recent))
        {
            return recent;
        }

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            // Whoever held the gate may have made the copy while this caller waited.
            if (TryGetRecent(key, out recent))
            {
                return recent;
            }

            var reference = await upload(cancellationToken);

            if (!string.IsNullOrWhiteSpace(reference))
            {
                Remember(key, reference);
            }

            return reference;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetRecent(string key, out string reference)
    {
        reference = null;

        if (!_recent.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.ExpiresUtc > _clock.UtcNow)
        {
            reference = entry.Reference;

            return true;
        }

        _recent.TryRemove(key, out _);

        return false;
    }

    private void Remember(string key, string reference)
    {
        var now = _clock.UtcNow;

        if (_recent.Count >= SweepThreshold)
        {
            foreach (var entry in _recent.Where(entry => entry.Value.ExpiresUtc <= now).ToArray())
            {
                _recent.TryRemove(entry.Key, out _);
            }
        }

        _recent[key] = (reference, now.Add(RecentUploadWindow));
    }
}
