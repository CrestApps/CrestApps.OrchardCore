using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// A fingerprint of the event fields that are synchronized with an external calendar. Comparing it with the one kept on the
/// event's <see cref="CalendarProviderMapping"/> tells whether the event changed here since it was last synchronized.
/// </summary>
public static class CalendarSyncFingerprint
{
    /// <summary>
    /// Computes the fingerprint of an event.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <returns>The fingerprint.</returns>
    public static string Compute(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        var builder = new StringBuilder();

        Append(builder, calendarEvent.Title);
        Append(builder, calendarEvent.Description);
        Append(builder, calendarEvent.Location);
        Append(builder, calendarEvent.AllDay ? "1" : "0");
        Append(builder, calendarEvent.Start.ToString("s", CultureInfo.InvariantCulture));
        Append(builder, calendarEvent.End.ToString("s", CultureInfo.InvariantCulture));
        Append(builder, calendarEvent.TimeZoneId);
        Append(builder, calendarEvent.Status.ToString());
        Append(builder, calendarEvent.Transparency.ToString());
        Append(builder, calendarEvent.Recurrence?.Rule);

        foreach (var date in calendarEvent.Recurrence?.ExceptionDates?.Order() ?? Enumerable.Empty<DateTime>())
        {
            Append(builder, date.ToString("s", CultureInfo.InvariantCulture));
        }

        foreach (var email in (calendarEvent.Attendees ?? []).Select(attendee => attendee.Email?.Trim().ToUpperInvariant()).Where(email => !string.IsNullOrEmpty(email)).Order(StringComparer.Ordinal))
        {
            Append(builder, email);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value?.Length ?? -1).Append(':').Append(value).Append('|');
    }
}
