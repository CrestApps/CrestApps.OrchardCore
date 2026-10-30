using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Core.ICalendar;

/// <summary>
/// Reads the <c>VEVENT</c>s of an RFC 5545 iCalendar document. Times with a <c>TZID</c> are read in that time zone (IANA or
/// Windows ids); times in UTC are converted to <paramref name="defaultTimeZoneId"/>; floating times are taken as wall-clock
/// times in it.
/// </summary>
public static class ICalendarReader
{
    /// <summary>
    /// The most events one document is read for.
    /// </summary>
    public const int MaxEvents = 10_000;

    /// <summary>
    /// Reads the events of a document.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="defaultTimeZoneId">The time zone for UTC and floating times.</param>
    /// <returns>The events.</returns>
    public static IReadOnlyList<ICalendarEventData> Read(string text, string defaultTimeZoneId)
    {
        var result = new List<ICalendarEventData>();
        ICalendarEventData current = null;
        var depth = 0;

        foreach (var line in Unfold(text ?? string.Empty))
        {
            var (name, parameters, value) = Parse(line);

            if (name is null)
            {
                continue;
            }

            if (name == "BEGIN")
            {
                if (value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase) && current is null)
                {
                    current = new ICalendarEventData { TimeZoneId = defaultTimeZoneId };
                    depth = 0;
                }
                else if (current is not null)
                {
                    depth++;
                }

                continue;
            }

            if (name == "END")
            {
                if (current is not null && depth > 0)
                {
                    depth--;
                }
                else if (current is not null && value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(current.Uid) && current.Start != default && result.Count < MaxEvents)
                    {
                        result.Add(current);
                    }

                    current = null;
                }

                continue;
            }

            if (current is null || depth > 0)
            {
                continue;
            }

            switch (name)
            {
                case "UID":
                    current.Uid = value.Trim();
                    break;
                case "SUMMARY":
                    current.Summary = Unescape(value);
                    break;
                case "DESCRIPTION":
                    current.Description = Unescape(value);
                    break;
                case "LOCATION":
                    current.Location = Unescape(value);
                    break;
                case "DTSTART":
                    if (TryReadTime(value, parameters, defaultTimeZoneId, out var start, out var startZone, out var isDate))
                    {
                        current.Start = start;
                        current.TimeZoneId = startZone;
                        current.AllDay = isDate;
                    }

                    break;
                case "DTEND":
                    if (TryReadTime(value, parameters, current.TimeZoneId ?? defaultTimeZoneId, out var end, out var endZone, out _))
                    {
                        current.End = endZone == current.TimeZoneId ? end : CalendarTime.ToLocal(CalendarTime.ToUtc(end, endZone), current.TimeZoneId);
                    }

                    break;
                case "DURATION":
                    if (current.Start != default && TryReadDuration(value, out var duration))
                    {
                        current.End = current.Start + duration;
                    }

                    break;
                case "RRULE":
                    current.Rule = value.Trim();
                    break;
                case "EXDATE":
                    foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (TryReadTime(item, parameters, current.TimeZoneId ?? defaultTimeZoneId, out var exception, out var exceptionZone, out _))
                        {
                            current.ExceptionDates.Add(exceptionZone == current.TimeZoneId ? exception : CalendarTime.ToLocal(CalendarTime.ToUtc(exception, exceptionZone), current.TimeZoneId));
                        }
                    }

                    break;
                case "RECURRENCE-ID":
                    if (TryReadTime(value, parameters, current.TimeZoneId ?? defaultTimeZoneId, out var recurrenceId, out var recurrenceZone, out _))
                    {
                        current.RecurrenceIdUtc = CalendarTime.ToUtc(recurrenceId, recurrenceZone);
                    }

                    break;
                case "STATUS":
                    current.Status = value.Trim().ToUpperInvariant() switch
                    {
                        "TENTATIVE" => CalendarEventStatus.Tentative,
                        "CANCELLED" => CalendarEventStatus.Cancelled,
                        _ => CalendarEventStatus.Confirmed,
                    };
                    break;
                case "TRANSP":
                    current.Transparency = value.Trim().Equals("TRANSPARENT", StringComparison.OrdinalIgnoreCase) ? CalendarTransparency.Free : CalendarTransparency.Busy;
                    break;
                case "SEQUENCE":
                    current.Sequence = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence) ? sequence : 0;
                    break;
                case "LAST-MODIFIED":
                    if (TryReadTime(value, parameters, "UTC", out var modified, out var modifiedZone, out _))
                    {
                        current.LastModifiedUtc = CalendarTime.ToUtc(modified, modifiedZone);
                    }

                    break;
                case "ATTENDEE":
                    var email = value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? value.Substring(7).Trim() : value.Trim();

                    if (!string.IsNullOrEmpty(email))
                    {
                        current.Attendees.Add(new CalendarAttendee
                        {
                            Email = email,
                            Name = parameters.TryGetValue("CN", out var cn) ? cn : null,
                            Role = parameters.TryGetValue("ROLE", out var role) && role.Equals("OPT-PARTICIPANT", StringComparison.OrdinalIgnoreCase) ? CalendarAttendeeRole.Optional : CalendarAttendeeRole.Required,
                            Response = parameters.TryGetValue("PARTSTAT", out var status) ? status.ToUpperInvariant() switch
                            {
                                "ACCEPTED" => CalendarAttendeeResponse.Accepted,
                                "DECLINED" => CalendarAttendeeResponse.Declined,
                                "TENTATIVE" => CalendarAttendeeResponse.Tentative,
                                _ => CalendarAttendeeResponse.NeedsAction,
                            } : CalendarAttendeeResponse.NeedsAction,
                        });
                    }

                    break;
            }
        }

        foreach (var item in result)
        {
            if (!item.End.HasValue || item.End.Value <= item.Start)
            {
                item.End = item.AllDay ? item.Start.AddDays(1) : item.Start.AddHours(item.End.HasValue ? 0 : 1);
            }

            if (!item.AllDay && item.End.Value <= item.Start)
            {
                item.End = item.Start.AddMinutes(1);
            }
        }

        return result;
    }

    internal static IEnumerable<string> Unfold(string text)
    {
        var builder = new StringBuilder();

        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t'))
            {
                builder.Append(raw, 1, raw.Length - 1);

                continue;
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }

            builder.Clear().Append(raw);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static (string Name, Dictionary<string, string> Parameters, string Value) Parse(string line)
    {
        var colon = -1;
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '"')
            {
                quoted = !quoted;
            }
            else if (line[index] == ':' && !quoted)
            {
                colon = index;
                break;
            }
        }

        if (colon <= 0)
        {
            return (null, null, null);
        }

        var head = line.Substring(0, colon).Split(';');
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in head.Skip(1))
        {
            var equals = parameter.IndexOf('=');

            if (equals > 0)
            {
                parameters[parameter.Substring(0, equals)] = parameter.Substring(equals + 1).Trim('"');
            }
        }

        return (head[0].ToUpperInvariant(), parameters, line.Substring(colon + 1));
    }

    private static bool TryReadTime(string value, Dictionary<string, string> parameters, string defaultTimeZoneId, out DateTime local, out string zoneId, out bool isDate)
    {
        local = default;
        zoneId = defaultTimeZoneId;
        isDate = false;

        var text = value.Trim();

        if ((parameters.TryGetValue("VALUE", out var kind) && kind.Equals("DATE", StringComparison.OrdinalIgnoreCase)) || text.Length == 8)
        {
            isDate = DateTime.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out local);

            return isDate;
        }

        var isUtc = text.EndsWith('Z') || text.EndsWith('z');

        if (!DateTime.TryParseExact(isUtc ? text.Substring(0, text.Length - 1) : text, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        if (isUtc)
        {
            local = CalendarTime.ToLocal(parsed, defaultTimeZoneId);
        }
        else if (parameters.TryGetValue("TZID", out var tzid) && CalendarTime.IsValidZone(tzid.Trim('/')))
        {
            zoneId = CalendarTime.NormalizeZoneId(tzid.Trim('/'));
            local = parsed;
        }
        else
        {
            local = parsed;
        }

        return true;
    }

    private static bool TryReadDuration(string value, out TimeSpan duration)
    {
        duration = default;

        try
        {
            duration = System.Xml.XmlConvert.ToTimeSpan(value.Trim());

            return duration > TimeSpan.Zero;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Unescape(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\\' && index + 1 < value.Length)
            {
                var next = value[index + 1];

                builder.Append(next is 'n' or 'N' ? '\n' : next);
                index++;
            }
            else
            {
                builder.Append(value[index]);
            }
        }

        return builder.ToString();
    }
}
