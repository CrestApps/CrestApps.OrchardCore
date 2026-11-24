using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Creates or updates a business-hours calendar, which is a Business Hours schedule, through the schedule manager, whose
/// handler validates it as it validates the schedule editor's post. It sets what the schedule editor sets: the time zone,
/// up to two opening windows per weekday, and the holidays and special days, each with an optional name, a special day
/// open only for its own hours. Queues, entry points and dialer profiles that name the schedule read it through the
/// shared business hours service, so they follow the change at once.
/// </summary>
internal sealed class SaveBusinessHoursCalendarTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveBusinessHoursCalendar";

    /// <summary>
    /// The most opening windows a weekday has, as the weekly hours editor offers them.
    /// </summary>
    public const int MaxWindowsPerDay = 2;

    private const int MinutesPerDay = 1440;

    private const string DateFormat = "yyyy-MM-dd";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": { "type": "string", "description": "The ID of the calendar to update. Leave it out to create one." },
        "name": { "type": "string", "description": "The calendar's name. Required when creating." },
        "description": { "type": "string", "description": "A description." },
        "timeZoneId": { "type": "string", "description": "The IANA time zone the hours and dates are read in, such as America/New_York (see listTimeZones). An empty text clears it; a new calendar starts in the site's time zone." },
        "enabled": { "type": "boolean", "description": "Whether the calendar gates routing. A disabled calendar is treated as always open." },
        "weeklySchedule": {
          "type": "array",
          "description": "Opening hours per weekday. Only the days given change, and the entries given for a day replace all of that day's opening hours: give a day twice for two windows, such as 09:00-12:00 and 13:00-17:00 (two at most). Required when creating; days left out of a new calendar are closed.",
          "items": {
            "type": "object",
            "properties": {
              "day": { "type": "string", "enum": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"], "description": "The weekday." },
              "open": { "type": "string", "description": "Opening time, HH:mm in the calendar's time zone." },
              "close": { "type": "string", "description": "Closing time, HH:mm; 24:00 for midnight. A closing time before the opening time runs past midnight." },
              "closed": { "type": "boolean", "description": "true when closed all day; then give the day once." }
            },
            "required": ["day"],
            "additionalProperties": false
          }
        },
        "holidays": { "type": "array", "items": { "type": "string" }, "description": "Dates closed all day, yyyy-MM-dd. Replaces the whole list of closed dates; a holiday that stays keeps its name, and a special day with hours keeps them unless its date is listed." },
        "addHolidays": { "type": "array", "items": { "type": "string" }, "description": "Dates to close all day, yyyy-MM-dd. A special day on such a date becomes a holiday." },
        "removeHolidays": { "type": "array", "items": { "type": "string" }, "description": "Dates to remove from the closed dates, yyyy-MM-dd." },
        "specialDays": {
          "type": "array",
          "description": "Every holiday and special day in full, as the schedule's Holidays and special days card lists them; replaces them all, holidays included. Use addSpecialDays and removeSpecialDays to change a few.",
          "items": {
            "type": "object",
            "properties": {
              "date": { "type": "string", "description": "The date, yyyy-MM-dd, in the calendar's time zone." },
              "closed": { "type": "boolean", "description": "true when closed all day (a holiday). Otherwise give open and close." },
              "open": { "type": "string", "description": "Opening time that day, HH:mm." },
              "close": { "type": "string", "description": "Closing time that day, HH:mm; 24:00 for midnight. The same time as open is open all day; an earlier one runs past midnight." },
              "name": { "type": "string", "description": "What the date is, such as Christmas Eve; optional." }
            },
            "required": ["date"],
            "additionalProperties": false
          }
        },
        "addSpecialDays": {
          "type": "array",
          "description": "Holidays and special days to add, each replacing whatever that date had: a date with its own opening hours (open and close), or closed all day (closed true), with an optional name.",
          "items": {
            "type": "object",
            "properties": {
              "date": { "type": "string", "description": "The date, yyyy-MM-dd, in the calendar's time zone." },
              "closed": { "type": "boolean", "description": "true when closed all day (a holiday). Otherwise give open and close." },
              "open": { "type": "string", "description": "Opening time that day, HH:mm." },
              "close": { "type": "string", "description": "Closing time that day, HH:mm; 24:00 for midnight. The same time as open is open all day; an earlier one runs past midnight." },
              "name": { "type": "string", "description": "What the date is, such as Christmas Eve; optional." }
            },
            "required": ["date"],
            "additionalProperties": false
          }
        },
        "removeSpecialDays": { "type": "array", "items": { "type": "string" }, "description": "Dates whose holiday or special hours are removed, so they follow the weekly hours again, yyyy-MM-dd." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a business-hours calendar, a Business Hours schedule: time zone, up to two opening windows per weekday, holiday dates, and special days with their own hours or closed, each with an optional name. Only the days and fields given change. Queues, entry points and automated sends that use it follow the change at once.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => BusinessHoursPermissions.ManageBusinessHoursSchedules;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>();
        var reader = new ContactCenterToolArguments(arguments);
        var id = reader.GetString("id");
        var schedule = id is null ? null : await manager.FindByIdAsync(id, cancellationToken);

        if (id is not null && schedule is null)
        {
            return $"Nothing was saved: no business-hours calendar has the ID '{id}'. Use listRecords with the kind businessHoursCalendar to find it.";
        }

        if (schedule is null && !reader.Has("weeklySchedule"))
        {
            return "Nothing was saved: give the weeklySchedule of a new calendar; a calendar with no opening hours is never open.";
        }

        reader.Text("name", nameof(BusinessHoursSchedule.Name), required: true);
        reader.Text("description", nameof(BusinessHoursSchedule.Description));
        reader.Bool("enabled", nameof(BusinessHoursSchedule.Enabled));

        // The hours are one value of the schedule: the current hours with the given time zone, days and dates changed.
        var current = schedule?.Hours ?? new HoursSchedule();
        var hours = new HoursSchedule
        {
            TimeZoneId = current.TimeZoneId,
            Windows = [.. (current.Windows ?? []).Where(window => window is not null)],
            DateExceptions = [.. (current.DateExceptions ?? []).Where(exception => exception is not null)],
        };

        var hoursChanged = ReadTimeZone(reader, hours, services.GetService<IClock>());
        hoursChanged |= ReadSchedule(reader, hours);
        hoursChanged |= ReadDateExceptions(reader, hours);

        if (reader.Errors.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", reader.Errors);
        }

        if (hoursChanged)
        {
            reader.Changes[nameof(BusinessHoursSchedule.Hours)] = JsonSerializer.SerializeToNode(hours, JOptions.Default);
        }

        var result = await ContactCenterCatalogWriter.SaveAsync(manager, schedule, reader.Changes, normalize: null, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Message;
        }

        return Json(new
        {
            saved = schedule is null ? "created" : "updated",
            changed = reader.Changes.Select(change => change.Key).ToList(),
            calendar = ContactCenterRecordKinds.DescribeCalendar(result.Record),
        });
    }

    private static bool ReadTimeZone(ContactCenterToolArguments reader, HoursSchedule hours, IClock clock)
    {
        if (!reader.Has("timeZoneId"))
        {
            return false;
        }

        var requested = reader.GetString("timeZoneId");

        if (requested is null)
        {
            hours.TimeZoneId = null;

            return true;
        }

        var zone = clock?.GetTimeZones().FirstOrDefault(candidate => string.Equals(candidate.TimeZoneId, requested, StringComparison.OrdinalIgnoreCase));

        if (zone is null)
        {
            reader.Errors.Add($"'{requested}' is not a known time zone. Use an IANA time zone ID such as America/New_York.");

            return false;
        }

        hours.TimeZoneId = zone.TimeZoneId;

        return true;
    }

    // The entries given for a day replace all of that day's opening windows: up to two windows when it opens, as the
    // weekly hours editor offers them, none when it is closed. The days not given keep theirs.
    private static bool ReadSchedule(ContactCenterToolArguments reader, HoursSchedule hours)
    {
        var rows = reader.NestedList("weeklySchedule");

        if (rows is null)
        {
            return false;
        }

        var days = new Dictionary<DayOfWeek, List<HoursWindow>>();
        var closedDays = new HashSet<DayOfWeek>();

        foreach (var row in rows)
        {
            if (!row.Has("day"))
            {
                reader.Errors.Add("Each weeklySchedule entry needs a day.");

                continue;
            }

            if (!row.TryReadChoice<DayOfWeek>("day", null, out var day) || day is null)
            {
                continue;
            }

            if (!days.TryGetValue(day.Value, out var windows))
            {
                windows = [];
                days[day.Value] = windows;
            }

            if (row.GetBool("closed") == true)
            {
                closedDays.Add(day.Value);

                continue;
            }

            var open = ReadMinutes(row.GetString("open"));
            var close = ReadMinutes(row.GetString("close"));

            if (open is null or MinutesPerDay || close is null)
            {
                reader.Errors.Add($"{day}: give open and close as HH:mm (close may be 24:00), or closed true.");

                continue;
            }

            windows.Add(new HoursWindow { Day = day.Value, OpenMinute = open.Value, CloseMinute = close.Value });
        }

        foreach (var (day, windows) in days)
        {
            if (closedDays.Contains(day) && windows.Count > 0)
            {
                reader.Errors.Add($"{day} is given both closed and with opening hours; give one or the other.");
            }
            else if (windows.Count > MaxWindowsPerDay)
            {
                reader.Errors.Add($"{day} has {windows.Count} opening windows; a day has {MaxWindowsPerDay} at most.");
            }
        }

        hours.Windows = [.. hours.Windows
            .Where(window => !days.ContainsKey(window.Day))
            .Concat(days.Values.SelectMany(windows => windows.OrderBy(window => window.OpenMinute)))];

        return true;
    }

    // The holidays and special days are the schedule's date exceptions, one per date: a holiday is closed all day, a
    // special day opens only for its own hours, and either may have a name. The holiday arguments change the closed
    // dates only; the special day arguments change any date. A date given two ways in one call is refused.
    private static bool ReadDateExceptions(ContactCenterToolArguments reader, HoursSchedule hours)
    {
        var replaceHolidays = ReadDates(reader, "holidays");
        var addHolidays = ReadDates(reader, "addHolidays");
        var removeHolidays = ReadDates(reader, "removeHolidays");
        var replaceSpecialDays = ReadSpecialDays(reader, "specialDays");
        var addSpecialDays = ReadSpecialDays(reader, "addSpecialDays");
        var removeSpecialDays = ReadDates(reader, "removeSpecialDays");

        if (replaceHolidays is null && addHolidays is null && removeHolidays is null &&
            replaceSpecialDays is null && addSpecialDays is null && removeSpecialDays is null)
        {
            return false;
        }

        if (replaceHolidays is not null && replaceSpecialDays is not null)
        {
            reader.Errors.Add("Give holidays or specialDays, not both: specialDays already lists the holidays.");

            return false;
        }

        // Each date may be given once across the lists, so a date is never both a holiday and a special day.
        var named = new Dictionary<DateOnly, string>();

        foreach (var (key, dates) in new (string Key, IEnumerable<DateOnly> Dates)[]
        {
            ("holidays", replaceHolidays ?? []),
            ("specialDays", (replaceSpecialDays ?? []).Select(day => day.Date)),
            ("addHolidays", addHolidays ?? []),
            ("removeHolidays", removeHolidays ?? []),
            ("addSpecialDays", (addSpecialDays ?? []).Select(day => day.Date)),
            ("removeSpecialDays", removeSpecialDays ?? []),
        })
        {
            foreach (var date in dates.Distinct())
            {
                if (named.TryGetValue(date, out var other))
                {
                    reader.Errors.Add($"{date.ToString(DateFormat, CultureInfo.InvariantCulture)} is in both {other} and {key}; give each date once.");
                }
                else
                {
                    named[date] = key;
                }
            }
        }

        if (reader.Errors.Count > 0)
        {
            return false;
        }

        var exceptions = hours.DateExceptions
            .GroupBy(exception => exception.Date)
            .ToDictionary(group => group.Key, group => group.Last());

        if (replaceSpecialDays is not null)
        {
            exceptions = replaceSpecialDays.ToDictionary(day => day.Date);
        }

        if (replaceHolidays is not null)
        {
            var listed = replaceHolidays.ToHashSet();

            foreach (var date in exceptions.Where(pair => pair.Value.Closed && !listed.Contains(pair.Key)).Select(pair => pair.Key).ToList())
            {
                exceptions.Remove(date);
            }

            foreach (var date in listed)
            {
                Close(exceptions, date);
            }
        }

        foreach (var date in addHolidays ?? [])
        {
            Close(exceptions, date);
        }

        foreach (var day in addSpecialDays ?? [])
        {
            exceptions[day.Date] = day;
        }

        foreach (var date in removeHolidays ?? [])
        {
            if (exceptions.TryGetValue(date, out var exception) && exception.Closed)
            {
                exceptions.Remove(date);
            }
        }

        foreach (var date in removeSpecialDays ?? [])
        {
            exceptions.Remove(date);
        }

        hours.DateExceptions = [.. exceptions.Values.OrderBy(exception => exception.Date)];

        return true;
    }

    // A date made a holiday keeps the name it had; its special hours, if any, are dropped.
    private static void Close(Dictionary<DateOnly, HoursDateException> exceptions, DateOnly date)
    {
        if (exceptions.TryGetValue(date, out var exception) && exception.Closed)
        {
            return;
        }

        exceptions[date] = new HoursDateException { Date = date, Name = exception?.Name, Closed = true };
    }

    private static List<HoursDateException> ReadSpecialDays(ContactCenterToolArguments reader, string key)
    {
        var rows = reader.NestedList(key);

        if (rows is null)
        {
            return null;
        }

        var days = new List<HoursDateException>();
        var seen = new HashSet<DateOnly>();

        foreach (var row in rows)
        {
            var text = row.GetString("date");

            if (!DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                reader.Errors.Add($"Each {key} entry needs a date in the form yyyy-MM-dd; '{text}' is not one.");

                continue;
            }

            var label = date.ToString(DateFormat, CultureInfo.InvariantCulture);

            if (!seen.Add(date))
            {
                reader.Errors.Add($"{label} is given twice in {key}; give each date once.");

                continue;
            }

            var name = row.GetString("name");

            // The schedule editor keeps a date's name on the date's own line.
            if (name is not null && name.IndexOfAny(['\r', '\n']) >= 0)
            {
                reader.Errors.Add($"{label}: the name must be one line.");

                continue;
            }

            if (row.GetBool("closed") == true)
            {
                if (row.Has("open") || row.Has("close"))
                {
                    reader.Errors.Add($"{label}: give closed true or open and close, not both.");

                    continue;
                }

                days.Add(new HoursDateException { Date = date, Name = name, Closed = true });

                continue;
            }

            var open = ReadMinutes(row.GetString("open"));
            var close = ReadMinutes(row.GetString("close"));

            if (open is null or MinutesPerDay || close is null)
            {
                reader.Errors.Add($"{label}: give open and close as HH:mm (close may be 24:00), or closed true.");

                continue;
            }

            days.Add(new HoursDateException { Date = date, Name = name, Closed = false, OpenMinute = open.Value, CloseMinute = close.Value });
        }

        return days;
    }

    private static List<DateOnly> ReadDates(ContactCenterToolArguments reader, string key)
    {
        var values = reader.GetStringList(key);

        if (values is null)
        {
            return null;
        }

        var dates = new List<DateOnly>();

        foreach (var value in values)
        {
            if (DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                dates.Add(date);
            }
            else
            {
                reader.Errors.Add($"'{value}' in {key} is not a date in the form yyyy-MM-dd.");
            }
        }

        return dates;
    }

    private static int? ReadMinutes(string value)
    {
        if (value is "24:00")
        {
            return MinutesPerDay;
        }

        return TimeOnly.TryParseExact(value, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? (time.Hour * 60) + time.Minute
            : null;
    }
}
