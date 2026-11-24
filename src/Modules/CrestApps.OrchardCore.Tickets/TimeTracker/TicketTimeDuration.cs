using System.Globalization;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// Reads time spent the ways people write it: <c>30</c> (minutes), <c>45m</c>, <c>1h 30m</c>, <c>2 hours 5 minutes</c>,
/// <c>1:30</c> and <c>1.5h</c> or <c>1,5 h</c>.
/// </summary>
public static partial class TicketTimeDuration
{
    /// <summary>
    /// Reads a duration.
    /// </summary>
    /// <param name="value">What the person wrote.</param>
    /// <returns>The minutes, rounded, or <see langword="null"/> when it isn't a duration of at least a minute.</returns>
    public static int? ParseMinutes(string value)
    {
        var text = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        double minutes;

        if (ClockPattern().Match(text) is { Success: true } clock)
        {
            var part = int.Parse(clock.Groups["m"].Value, CultureInfo.InvariantCulture);

            if (part > 59)
            {
                return null;
            }

            minutes = (int.Parse(clock.Groups["h"].Value, CultureInfo.InvariantCulture) * 60) + part;
        }
        else if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var plain))
        {
            minutes = plain;
        }
        else if (UnitsPattern().Match(text) is { Success: true } units && (units.Groups["h"].Success || units.Groups["m"].Success))
        {
            minutes = (Number(units.Groups["h"]) * 60) + Number(units.Groups["m"]) + Number(units.Groups["rest"]);
        }
        else
        {
            return null;
        }

        var rounded = (int)Math.Round(minutes, MidpointRounding.AwayFromZero);

        return rounded >= 1 ? rounded : null;
    }

    private static double Number(Group group)
        => group.Success ? double.Parse(group.Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture) : 0;

    [GeneratedRegex(@"^(?<h>\d{1,2}):(?<m>\d{2})$")]
    private static partial Regex ClockPattern();

    // "1h 30m", "1h30", "2 hours 5 minutes", "45 min", "1.5h": hours, then minutes, or minutes alone. A bare number
    // after the hours is minutes.
    [GeneratedRegex(@"^(?:(?<h>\d+(?:[.,]\d+)?)\s*(?:h|hr|hrs|hour|hours))?\s*(?:(?<m>\d+)\s*(?:m|min|mins|minute|minutes)|(?<rest>\d+))?$")]
    private static partial Regex UnitsPattern();
}
