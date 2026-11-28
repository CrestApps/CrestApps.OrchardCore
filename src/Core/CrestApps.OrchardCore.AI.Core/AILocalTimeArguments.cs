using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Reads the date-times AI tools take as wall-clock times in the user's or the site's time zone (never UTC), as the admin
/// forms take them, and converts them to UTC with <see cref="LocalClockExtensions.ToUtcLenientAsync"/>, so a time a
/// daylight-saving change skips or repeats is accepted.
/// </summary>
public static class AILocalTimeArguments
{
    // The formats a local date-time argument is accepted in; a date alone means midnight.
    private static readonly string[] _formats = ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"];

    /// <summary>
    /// Reads a time that already happened, such as when a payment was received, entered in the local time zone.
    /// </summary>
    /// <param name="localClock">The local clock, whose time zone the text is in.</param>
    /// <param name="clock">The clock that says what time it is now.</param>
    /// <param name="text">The text; blank means the argument was not given.</param>
    /// <param name="refused">How a refusal starts, such as "Nothing was recorded".</param>
    /// <param name="argumentName">The argument's name, such as <c>receivedAt</c>.</param>
    /// <param name="what">What the time is, for the refusal of a time in the future, such as "the time the payment was received".</param>
    /// <returns>The UTC time, <see langword="null"/> when nothing was given, or the sentence that says why it is refused.</returns>
    public static async Task<(DateTime? Utc, string Error)> ReadPastAsync(ILocalClock localClock, IClock clock, string text, string refused, string argumentName, string what)
    {
        ArgumentNullException.ThrowIfNull(localClock);
        ArgumentNullException.ThrowIfNull(clock);

        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null);
        }

        if (!DateTime.TryParseExact(text.Trim(), _formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return (null, $"{refused}: give {argumentName} as yyyy-MM-ddTHH:mm in the site's time zone.");
        }

        var utc = DateTime.SpecifyKind(await localClock.ToUtcLenientAsync(DateTime.SpecifyKind(local, DateTimeKind.Unspecified)), DateTimeKind.Utc);

        return utc > clock.UtcNow
            ? (null, $"{refused}: {what} can't be in the future.")
            : (utc, null);
    }
}
