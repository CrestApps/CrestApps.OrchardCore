namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Gathers the keys a caller presses into one entry.
/// </summary>
/// <remarks>
/// Providers report each key as its own event, and a caller typing "1234" sends four of them within a fraction of a
/// second. Answered one at a time, that was four replies asked for at once, each about a single digit. So keys are
/// collected until the caller stops pressing, and answered together.
/// <para>
/// Each key's webhook can be handled on its own request, and those do not always finish in the order the keys were
/// pressed. So a key's time, when the provider reports it, decides its place.
/// </para>
/// </remarks>
internal sealed class CallerKeyBuffer
{
    private readonly Lock _lock = new();
    private readonly List<(DateTime? PressedUtc, string Keys)> _keys = [];
    private bool _collecting;

    /// <summary>
    /// Adds keys the caller pressed.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <param name="pressedUtc">When the provider says they were pressed, if it says.</param>
    /// <returns>
    /// <see langword="true"/> when these keys start a new entry, so the caller is the one to take it with
    /// <see cref="TakeWhenSettledAsync"/>; <see langword="false"/> when they joined an entry already being collected.
    /// </returns>
    public bool Add(string keys, DateTime? pressedUtc = null)
    {
        lock (_lock)
        {
            _keys.Add((pressedUtc, keys));

            if (_collecting)
            {
                return false;
            }

            _collecting = true;

            return true;
        }
    }

    /// <summary>
    /// Waits until no key has been pressed for <paramref name="settle"/>, then returns every key collected and starts
    /// a new entry.
    /// </summary>
    /// <param name="settle">How long the caller must stop pressing keys.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The keys, in the order they were pressed.</returns>
    public async Task<string> TakeWhenSettledAsync(TimeSpan settle, CancellationToken cancellationToken)
    {
        int seen;
        int now;

        do
        {
            lock (_lock)
            {
                seen = _keys.Count;
            }

            await Task.Delay(settle, cancellationToken);

            lock (_lock)
            {
                now = _keys.Count;
            }
        }
        while (now != seen);

        lock (_lock)
        {
            // Ordered by time only when every key has one; otherwise the order they arrived in is the best there is.
            var ordered = _keys.TrueForAll(key => key.PressedUtc.HasValue)
                ? _keys.OrderBy(key => key.PressedUtc.Value)
                : _keys.AsEnumerable();

            var keys = string.Concat(ordered.Select(key => key.Keys));
            _keys.Clear();
            _collecting = false;

            return keys;
        }
    }
}
