using System.Collections.Concurrent;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// Keeps automated posts out of the ticket web form: a hidden trap field a person never fills, and a limit of
/// <see cref="SubmissionsPerWindow"/> requests per visitor address in <see cref="Window"/>. A challenge (ReCaptcha) is
/// added on top when that feature is on.
/// </summary>
public sealed class TicketFormGuard
{
    /// <summary>
    /// The name of the hidden trap field.
    /// </summary>
    public const string TrapFieldName = "website";

    /// <summary>
    /// How many requests a visitor may send in a window.
    /// </summary>
    public const int SubmissionsPerWindow = 5;

    /// <summary>
    /// The window the limit counts in.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, (DateTime Start, int Count)> _counts = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    public TicketFormGuard(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Returns whether a post looks automated: its trap field is filled.
    /// </summary>
    /// <param name="trapValue">The trap field's value.</param>
    /// <returns>Whether to refuse it.</returns>
    public static bool IsTrapped(string trapValue)
        => !string.IsNullOrWhiteSpace(trapValue);

    /// <summary>
    /// Counts a request from an address and returns whether it is within the limit.
    /// </summary>
    /// <param name="address">The visitor's address (or another key for them).</param>
    /// <returns>Whether the request may be taken.</returns>
    public bool TryTake(string address)
    {
        var key = string.IsNullOrEmpty(address) ? "unknown" : address;
        var now = _clock.UtcNow;
        var allowed = true;

        _counts.AddOrUpdate(
            key,
            _ => (now, 1),
            (_, current) =>
            {
                if (now - current.Start >= Window)
                {
                    return (now, 1);
                }

                allowed = current.Count < SubmissionsPerWindow;

                return allowed ? (current.Start, current.Count + 1) : current;
            });

        // Old windows are dropped now and then, so the counts don't grow without end.
        if (_counts.Count > 10_000)
        {
            foreach (var (stale, value) in _counts.Where(pair => now - pair.Value.Start >= Window).ToList())
            {
                _counts.TryRemove(stale, out _);
            }
        }

        return allowed;
    }
}

/// <summary>
/// A challenge the ticket web form shows and checks, when one is set up.
/// </summary>
public interface ITicketFormChallenge
{
    /// <summary>
    /// Gets whether the challenge is set up, so the form shows it.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Checks the posted challenge.
    /// </summary>
    /// <returns>Whether it passed.</returns>
    Task<bool> VerifyAsync();
}
