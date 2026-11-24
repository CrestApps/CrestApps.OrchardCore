using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Recurrence;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Changes an event the person can change, or occurrences of a repeating event, with the fields of the event editor.
/// </summary>
internal sealed class UpdateCalendarEventTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "updateCalendarEvent";

    private static readonly string[] _attendeeFields = ["attendeeUserIds", "guestEmails", "resourceCalendarIds"];

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "eventId": { "type": "string", "description": "The event's ID, from findCalendarEvents or getCalendarEvent." },
        "occurrenceStart": { "type": "string", "description": "For a repeating event, the occurrenceStart findCalendarEvents returned for the occurrence to change. Leave out to change the whole series." },
        "scope": { "type": "string", "enum": ["ThisOccurrence", "ThisAndFollowing", "AllOccurrences"], "description": "With occurrenceStart: change only that occurrence, that one and the following ones, or all of them. Defaults to ThisOccurrence." },
        "title": { "type": "string", "description": "The title." },
        "eventType": { "type": "string", "description": "The event type, one of the calendar's event types (see getCalendar)." },
        "allDay": { "type": "boolean", "description": "Whether the event lasts whole days. To make an event all-day, give start (and end) as dates." },
        "start": { "type": "string", "description": "The new start: an ISO 8601 time with an offset, or a date (yyyy-MM-dd) for an all-day event." },
        "end": { "type": "string", "description": "The new end: an ISO 8601 time with an offset, or the last day of an all-day event. Leave out to keep the length." },
        "timeZoneId": { "type": "string", "description": "The event's IANA time zone, such as Europe/Paris." },
        "location": { "type": "string", "description": "Where it takes place. An empty text clears it." },
        "description": { "type": "string", "description": "The description. An empty text clears it." },
        "status": { "type": "string", "enum": ["Confirmed", "Tentative", "Cancelled"], "description": "Confirming a tentative event needs the right to manage its calendar. Prefer cancelCalendarEvent to cancel." },
        "transparency": { "type": "string", "enum": ["Busy", "Free"], "description": "Whether the event makes the attendees busy." },
        "visibility": { "type": "string", "enum": ["Default", "Public", "Private"], "description": "Private events show only as busy time to people who can only read the calendar." },
        "color": { "type": "string", "description": "A #rrggbb color for this event. An empty text uses the calendar's color." },
        "categoryId": { "type": "string", "description": "A category ID from listCalendarCategories. An empty text removes the category." },
        "reminderMinutes": { "type": "integer", "enum": [-1, 0, 5, 10, 15, 30, 60, 120, 1440], "description": "Minutes before the start to remind the attendees on screen (Calendar Reminders feature). -1 removes the reminder." },
        "recurrence": { "type": "string", "description": "An iCalendar RRULE for the whole series, such as FREQ=WEEKLY;BYDAY=MO,WE;UNTIL=20270331. An empty text stops the event repeating. Not with occurrenceStart." },
        "attendeeUserIds": { "type": "array", "items": { "type": "string" }, "description": "The user IDs of every user who should attend. Replaces the users invited now; leave out to keep them." },
        "guestEmails": { "type": "array", "items": { "type": "string" }, "description": "The email addresses of every outside guest. Replaces the guests invited now; leave out to keep them." },
        "resourceCalendarIds": { "type": "array", "items": { "type": "string" }, "description": "The IDs of every room or equipment calendar to book. Replaces the resources booked now; leave out to keep them." }
      },
      "required": ["eventId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Changes an event's title, type, times, time zone, location, description, status, busy/free, visibility, category, reminders, recurrence or attendees; for a repeating event say which occurrences (this one, this and following, all). Fields left out keep their value. Attendees with an email address are emailed about the change when Calendar Invitations is on. Use moveCalendarEvent when only the time changes.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (calendarEvent, calendar) = await GetCalendarEventTool.LoadAsync(services, Read(arguments, "eventId"), cancellationToken);

        if (calendarEvent is null || !await services.GetRequiredService<CalendarEventEditor>().CanChangeAsync(user, calendarEvent, calendar))
        {
            return "The user can't change that event.";
        }

        // Only an occurrence of a series is edited by occurrence, as in the event editor.
        DateTime? originalStartUtc = null;
        var occurrence = Read(arguments, "occurrenceStart");

        if (occurrence is not null)
        {
            if (calendarEvent.SeriesId is not null || calendarEvent.Recurrence is null)
            {
                return "occurrenceStart applies only to a repeating event. Leave it out to change this event.";
            }

            if (!DateTimeOffset.TryParse(occurrence, out var parsed))
            {
                return "occurrenceStart must be the ISO 8601 time findCalendarEvents returned.";
            }

            originalStartUtc = parsed.UtcDateTime;
        }

        var scope = CalendarEditScope.ThisOccurrence;

        if (Read(arguments, "scope") is { } scopeText && (originalStartUtc is null || !Enum.TryParse(scopeText, ignoreCase: true, out scope)))
        {
            return originalStartUtc is null
                ? "scope applies only with occurrenceStart."
                : "scope must be ThisOccurrence, ThisAndFollowing or AllOccurrences.";
        }

        var (error, change) = await PrepareAsync(arguments, services, user, calendarEvent, calendar, originalStartUtc.HasValue);

        if (error is not null)
        {
            return error;
        }

        var eventManager = services.GetRequiredService<ICalendarEventManager>();
        var options = new CalendarChangeOptions { Actor = CalendarUser.GetId(user) };
        var result = originalStartUtc.HasValue
            ? await eventManager.UpdateOccurrenceAsync(calendarEvent, originalStartUtc.Value, scope, change, options, cancellationToken)
            : await eventManager.UpdateAsync(calendarEvent, change, options, cancellationToken);

        if (!result.Succeeded)
        {
            return "The event was not changed: " + CalendarToolSupport.Describe(result.Errors);
        }

        var saved = result.Value ?? calendarEvent;
        var savedCalendar = saved.CalendarId == calendar.ItemId ? calendar : await services.GetRequiredService<ICalendarService>().FindByIdAsync(saved.CalendarId, cancellationToken) ?? calendar;

        return CalendarToolSupport.Json(new
        {
            changed = originalStartUtc.HasValue ? scope.ToString() : "Event",
            calendarEvent = await CalendarToolResults.DescribeEventAsync(services, user, saved, savedCalendar),
        });
    }

    // Checks every argument and returns the change to apply, so a refused call changes nothing.
    private static async Task<(string Error, Action<CalendarEvent> Change)> PrepareAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CalendarEvent calendarEvent, Calendar calendar, bool isOccurrence)
    {
        var input = new CalendarEventInput
        {
            Title = CalendarToolSupport.ReadRaw(arguments, "title"),
            EventType = Read(arguments, "eventType"),
            AllDay = CalendarToolSupport.ReadBool(arguments, "allDay"),
            Start = Read(arguments, "start"),
            End = Read(arguments, "end"),
            Location = CalendarToolSupport.ReadRaw(arguments, "location"),
            Description = CalendarToolSupport.ReadRaw(arguments, "description"),
            Status = Read(arguments, "status"),
            Transparency = Read(arguments, "transparency"),
        };

        if (input.Title is { Length: 0 })
        {
            return ("The title is required.", null);
        }

        if (Read(arguments, "timeZoneId") is { } timeZoneId)
        {
            input.TimeZoneId = CalendarTime.NormalizeZoneId(timeZoneId);

            if (input.TimeZoneId is null || !CalendarTime.IsValidZone(input.TimeZoneId))
            {
                return ($"'{timeZoneId}' is not a time zone. Use an IANA time zone such as Europe/Paris.", null);
            }
        }

        if (input.EventType is not null && services.GetRequiredService<ICalendarTypeManager>().FindEventType(calendar.Type, input.EventType) is null)
        {
            return ($"'{input.EventType}' is not an event type of {calendar.Type} calendars. Use getCalendar to see them.", null);
        }

        if (input.Status is not null)
        {
            if (!Enum.TryParse<CalendarEventStatus>(input.Status, ignoreCase: true, out var status))
            {
                return ("The status must be Confirmed, Tentative or Cancelled.", null);
            }

            // Confirming a tentative event is the calendar manager's Confirm action.
            if (status == CalendarEventStatus.Confirmed && calendarEvent.Status != CalendarEventStatus.Confirmed &&
                await services.GetRequiredService<ICalendarAccessService>().GetAccessLevelAsync(user, calendar) < CalendarAccessLevel.Manage)
            {
                return ("Only someone who manages the calendar can confirm a tentative event.", null);
            }
        }

        if (input.Transparency is not null && !Enum.TryParse<CalendarTransparency>(input.Transparency, ignoreCase: true, out _))
        {
            return ("transparency must be Busy or Free.", null);
        }

        CalendarEventVisibility? visibility = null;

        if (Read(arguments, "visibility") is { } visibilityText)
        {
            if (!Enum.TryParse<CalendarEventVisibility>(visibilityText, ignoreCase: true, out var parsedVisibility))
            {
                return ("visibility must be Default, Public or Private.", null);
            }

            visibility = parsedVisibility;
        }

        var color = CalendarToolSupport.ReadRaw(arguments, "color");

        if (color is { Length: > 0 } && !CalendarCategoryRules.IsValidColor(color))
        {
            return ("The color must be a #rrggbb color.", null);
        }

        var categoryId = CalendarToolSupport.ReadRaw(arguments, "categoryId");

        if (categoryId is { Length: > 0 } && categoryId != calendarEvent.CategoryId)
        {
            var categories = await services.GetRequiredService<ICalendarCategoryService>().ListAsync(CalendarUser.GetId(user));

            if (!categories.Any(category => category.ItemId == categoryId))
            {
                return ("There is no category with that ID that the user can use. Use listCalendarCategories to find it.", null);
            }
        }

        IList<int> reminders = null;

        if (CalendarToolSupport.Has(arguments, "reminderMinutes"))
        {
            var minutes = CalendarToolSupport.ReadInt(arguments, "reminderMinutes");
            var featuresManager = services.GetService<IShellFeaturesManager>();

            if (featuresManager is null || !await featuresManager.IsFeatureEnabledAsync(CalendarsConstants.Features.Reminders))
            {
                return ("Reminders need the Calendar Reminders feature, which is off.", null);
            }

            if (minutes is not { } value || (value != -1 && !CalendarReminders.IsChoice(value)))
            {
                return ($"reminderMinutes must be -1 (no reminder) or one of {string.Join(", ", CalendarReminders.Choices)}.", null);
            }

            reminders = value == -1 ? [] : [value];
        }

        var recurrence = CalendarToolSupport.ReadRaw(arguments, "recurrence");

        if (recurrence is not null)
        {
            if (isOccurrence || calendarEvent.SeriesId is not null)
            {
                return ("The repeat rule belongs to the whole series: change it without occurrenceStart, on the series' event.", null);
            }

            if (recurrence.Length > 0 && !RecurrenceRule.TryParse(recurrence.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase) ? recurrence[6..] : recurrence, out _, out var ruleError))
            {
                return ($"The repeat rule is not valid: {ruleError}", null);
            }

            input.Recurrence = recurrence;
        }

        // The times are read on a copy first: the input applier stops at a time it cannot read.
        var errors = new List<string>();

        CalendarEventInputApplier.Apply(input, Clone(calendarEvent), errors);

        if (errors.Count > 0)
        {
            return (string.Join(" ", errors), null);
        }

        var (attendeeError, attendees) = await BuildAttendeesAsync(arguments, services, calendarEvent);

        if (attendeeError is not null)
        {
            return (attendeeError, null);
        }

        return (null, target =>
        {
            CalendarEventInputApplier.Apply(input, target, []);

            // As in the event editor, a cleared text is stored as no text.
            if (input.Location is { Length: 0 })
            {
                target.Location = null;
            }

            if (input.Description is { Length: 0 })
            {
                target.Description = null;
            }

            if (visibility.HasValue)
            {
                target.Visibility = visibility.Value;
            }

            if (color is not null)
            {
                target.Color = color.Length == 0 ? null : color;
            }

            if (categoryId is not null)
            {
                target.CategoryId = categoryId.Length == 0 ? null : categoryId;
            }

            if (reminders is not null)
            {
                target.ReminderMinutes = reminders.ToList();
            }

            if (attendees is not null)
            {
                target.Attendees = attendees.ToList();
            }
        });
    }

    // Each kind of attendee given replaces that kind; the kinds left out keep the attendees they have.
    private static async Task<(string Error, List<CalendarAttendee> Attendees)> BuildAttendeesAsync(AIFunctionArguments arguments, IServiceProvider services, CalendarEvent calendarEvent)
    {
        if (!_attendeeFields.Any(field => CalendarToolSupport.Has(arguments, field)))
        {
            return (null, null);
        }

        var userLookup = services.GetService<CalendarUserLookup>();

        if (userLookup is null)
        {
            return ("Attendees can't be changed here: the users of the site can't be read.", null);
        }

        var userIds = CalendarToolSupport.Has(arguments, "attendeeUserIds") ? ReadList(arguments, "attendeeUserIds").Distinct(StringComparer.Ordinal).ToList() : null;
        var guests = CalendarToolSupport.Has(arguments, "guestEmails") ? ReadList(arguments, "guestEmails") : null;
        var resourceIds = CalendarToolSupport.Has(arguments, "resourceCalendarIds") ? ReadList(arguments, "resourceCalendarIds") : null;

        if (userIds is not null)
        {
            var found = await userLookup.GetAsync(userIds);
            var missing = userIds.FirstOrDefault(id => !found.Any(item => string.Equals(item.Value, id, StringComparison.Ordinal)));

            if (missing is not null)
            {
                return ($"There is no user with the ID '{missing}'. Give user IDs, not names; give outside guests in guestEmails.", null);
            }
        }

        string invalid = null;
        var built = await CalendarAttendeeList.BuildAsync(
            calendarEvent.Attendees,
            userIds,
            guests,
            resourceIds,
            userLookup,
            services.GetRequiredService<ICalendarService>(),
            line => invalid ??= line);

        if (invalid is not null)
        {
            return ($"'{invalid}' is not an email address.", null);
        }

        var attendees = new List<CalendarAttendee>();

        attendees.AddRange(userIds is null ? calendarEvent.Attendees.Where(IsUser) : built.Where(IsUser));
        attendees.AddRange(guests is null ? calendarEvent.Attendees.Where(IsGuest) : built.Where(IsGuest));
        attendees.AddRange(resourceIds is null ? calendarEvent.Attendees.Where(IsResource) : built.Where(IsResource));

        return (null, attendees);
    }

    private static bool IsResource(CalendarAttendee attendee)
        => !string.IsNullOrEmpty(attendee.ResourceCalendarId);

    private static bool IsUser(CalendarAttendee attendee)
        => !IsResource(attendee) && !string.IsNullOrEmpty(attendee.UserId);

    private static bool IsGuest(CalendarAttendee attendee)
        => !IsResource(attendee) && !IsUser(attendee);

    private static CalendarEvent Clone(CalendarEvent calendarEvent)
        => JsonSerializer.Deserialize<CalendarEvent>(JsonSerializer.Serialize(calendarEvent));
}
