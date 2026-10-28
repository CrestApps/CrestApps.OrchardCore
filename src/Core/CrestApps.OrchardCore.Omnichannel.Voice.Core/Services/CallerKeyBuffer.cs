using System.Text;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Gathers the keys a caller presses into one entry.
/// </summary>
/// <remarks>
/// Providers report each key as its own event, and a caller typing "1234" sends four of them within a fraction of a
/// second. Answered one at a time, that was four replies asked for at once, each about a single digit. So keys are
/// collected until the caller stops pressing, and answered together.
/// </remarks>
internal sealed class CallerKeyBuffer
{
    private readonly Lock _lock = new();
    private readonly StringBuilder _keys = new();
    private bool _collecting;

    /// <summary>
    /// Adds keys the caller pressed.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <returns>
    /// <see langword="true"/> when these keys start a new entry, so the caller is the one to take it with
    /// <see cref="TakeWhenSettledAsync"/>; <see langword="false"/> when they joined an entry already being collected.
    /// </returns>
    public bool Add(string keys)
    {
        lock (_lock)
        {
            _keys.Append(keys);

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
                seen = _keys.Length;
            }

            await Task.Delay(settle, cancellationToken);

            lock (_lock)
            {
                now = _keys.Length;
            }
        }
        while (now != seen);

        lock (_lock)
        {
            var keys = _keys.ToString();
            _keys.Clear();
            _collecting = false;

            return keys;
        }
    }
}
