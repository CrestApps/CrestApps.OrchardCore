using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Endpoints;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.Controllers;

/// <summary>
/// The pages that create, change, cancel and answer events.
/// </summary>
public sealed class CalendarEventsController : Controller
{
    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventManager _eventManager;
    private readonly ICalendarAccessService _accessService;
    private readonly ICalendarTypeManager _typeManager;
    private readonly CalendarEventEditor _editor;
    private readonly IDisplayManager<CalendarEvent> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventsController"/> class.
    /// </summary>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="eventManager">The event manager.</param>
    /// <param name="accessService">The calendar access service.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <param name="editor">The event editing rules.</param>
    /// <param name="displayManager">The event display manager.</param>
    /// <param name="updateModelAccessor">The model updater accessor.</param>
    /// <param name="localizerFactory">The localizer factory.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarEventsController(
        ICalendarService calendarService,
        ICalendarEventManager eventManager,
        ICalendarAccessService accessService,
        ICalendarTypeManager typeManager,
        CalendarEventEditor editor,
        IDisplayManager<CalendarEvent> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IStringLocalizerFactory localizerFactory,
        INotifier notifier,
        IHtmlLocalizer<CalendarEventsController> htmlLocalizer,
        IStringLocalizer<CalendarEventsController> stringLocalizer)
    {
        _calendarService = calendarService;
        _eventManager = eventManager;
        _accessService = accessService;
        _typeManager = typeManager;
        _editor = editor;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _localizerFactory = localizerFactory;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Asks which calendar and kind of event to add.
    /// </summary>
    /// <param name="start">The start, passed on to the editor.</param>
    /// <param name="end">The end, passed on to the editor.</param>
    /// <param name="allDay">Whether the event lasts whole days.</param>
    /// <returns>The page.</returns>
    [Admin("calendar/events/choose", "CalendarEventsChoose")]
    public async Task<IActionResult> Choose(string start, string end, bool allDay)
    {
        var writable = await _accessService.ListAccessibleAsync(User, CalendarAccessLevel.Write);
        var model = new ChooseCalendarViewModel
        {
            Start = start,
            End = end,
            AllDay = allDay,
        };

        foreach (var access in writable.Where(access => access.Calendar.Enabled))
        {
            var type = _typeManager.FindType(access.Calendar.Type);

            model.Calendars.Add(new ChooseCalendarEntry
            {
                Id = access.Calendar.ItemId,
                Name = access.Calendar.Name,
                Color = access.Calendar.Color ?? type?.DefaultColor,
                Icon = type?.Icon ?? "fa-solid fa-calendar",
                EventTypes = _typeManager.ListEventTypes(access.Calendar.Type)
                    .Select(eventType => new KeyValuePair<string, string>(eventType.Name, _localizerFactory.Localize(eventType.DisplayName).Value))
                    .ToList(),
            });
        }

        return View(model);
    }

    /// <summary>
    /// Shows the editor for a new event.
    /// </summary>
    /// <param name="calendarId">The calendar.</param>
    /// <param name="eventType">The kind of event.</param>
    /// <param name="start">When it starts: an ISO 8601 time, or a date for an all-day event.</param>
    /// <param name="end">When it ends.</param>
    /// <param name="allDay">Whether it lasts whole days.</param>
    /// <returns>The editor.</returns>
    [Admin("calendar/events/create", "CalendarEventsCreate")]
    public async Task<IActionResult> Create(string calendarId, string eventType, string start, string end, bool allDay)
    {
        var calendar = await _calendarService.FindByIdAsync(calendarId);

        if (calendar is null)
        {
            return RedirectToAction(nameof(Choose), new { start, end, allDay });
        }

        if (!await _editor.CanAddAsync(User, calendar))
        {
            return Forbid();
        }

        var calendarEvent = await NewEventAsync(calendar, eventType, start, end, allDay);

        return View(new EditCalendarEventViewModel
        {
            CalendarId = calendar.ItemId,
            CalendarName = calendar.Name,
            IsNew = true,
            Title = S["New Event"],
            Editor = await _displayManager.BuildEditorAsync(calendarEvent, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Creates an event.
    /// </summary>
    /// <param name="model">The page's fields.</param>
    /// <returns>A redirect to the calendar, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("calendar/events/create", "CalendarEventsCreate")]
    public async Task<IActionResult> CreatePost(EditCalendarEventViewModel model)
    {
        var calendar = await _calendarService.FindByIdAsync(model.CalendarId);

        if (calendar is null)
        {
            return NotFound();
        }

        if (!await _editor.CanAddAsync(User, calendar))
        {
            return Forbid();
        }

        var calendarEvent = await _eventManager.NewAsync(calendar);
        var editor = await _displayManager.UpdateEditorAsync(calendarEvent, _updateModelAccessor.ModelUpdater, isNew: true);

        if (ModelState.IsValid)
        {
            var result = await _eventManager.CreateAsync(calendarEvent, new CalendarChangeOptions { Actor = CalendarUser.GetId(User) });

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The event has been added."]);

                return RedirectToCalendar(calendarEvent);
            }

            CalendarEventEditor.AddErrors(ModelState, result.Errors);
        }

        return View(new EditCalendarEventViewModel
        {
            CalendarId = calendar.ItemId,
            CalendarName = calendar.Name,
            IsNew = true,
            Title = S["New Event"],
            Editor = editor,
        });
    }

    /// <summary>
    /// Shows the editor of an event, or of one occurrence of a repeating event.
    /// </summary>
    /// <param name="id">The event's id.</param>
    /// <param name="occurrence">When the occurrence was due to start, as an ISO 8601 UTC time.</param>
    /// <returns>The editor.</returns>
    [Admin("calendar/events/edit/{id}", "CalendarEventsEdit")]
    public async Task<IActionResult> Edit(string id, string occurrence)
    {
        var (calendarEvent, calendar) = await LoadAsync(id);

        if (calendarEvent is null)
        {
            return NotFound();
        }

        if (!await _editor.CanChangeAsync(User, calendarEvent, calendar))
        {
            return Forbid();
        }

        var originalStartUtc = ParseOccurrence(calendarEvent, occurrence);
        var target = originalStartUtc.HasValue ? CreateOccurrenceCopy(calendarEvent, originalStartUtc.Value) : calendarEvent;

        return View(await BuildEditModelAsync(calendarEvent, calendar, occurrence, originalStartUtc.HasValue, await _displayManager.BuildEditorAsync(target, _updateModelAccessor.ModelUpdater, isNew: false)));
    }

    /// <summary>
    /// Saves changes to an event, or to occurrences of a repeating event.
    /// </summary>
    /// <param name="id">The event's id.</param>
    /// <param name="model">The page's fields.</param>
    /// <returns>A redirect to the calendar, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("calendar/events/edit/{id}", "CalendarEventsEdit")]
    public async Task<IActionResult> EditPost(string id, EditCalendarEventViewModel model)
    {
        var (calendarEvent, calendar) = await LoadAsync(id);

        if (calendarEvent is null)
        {
            return NotFound();
        }

        if (!await _editor.CanChangeAsync(User, calendarEvent, calendar))
        {
            return Forbid();
        }

        var originalStartUtc = ParseOccurrence(calendarEvent, model.Occurrence);
        var copy = originalStartUtc.HasValue ? CreateOccurrenceCopy(calendarEvent, originalStartUtc.Value) : CloneForEditing(calendarEvent);
        var editor = await _displayManager.UpdateEditorAsync(copy, _updateModelAccessor.ModelUpdater, isNew: false);

        if (ModelState.IsValid)
        {
            var options = new CalendarChangeOptions { Actor = CalendarUser.GetId(User) };
            var result = originalStartUtc.HasValue
                ? await _eventManager.UpdateOccurrenceAsync(calendarEvent, originalStartUtc.Value, Enum.TryParse<CalendarEditScope>(model.Scope, out var scope) ? scope : CalendarEditScope.ThisOccurrence, target => CalendarEventEditor.CopyEditableFields(copy, target, includeRecurrence: false), options)
                : await _eventManager.UpdateAsync(calendarEvent, target => CalendarEventEditor.CopyEditableFields(copy, target, includeRecurrence: true), options);

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The event has been saved."]);

                return RedirectToCalendar(result.Value);
            }

            CalendarEventEditor.AddErrors(ModelState, result.Errors);
        }

        return View(await BuildEditModelAsync(calendarEvent, calendar, model.Occurrence, originalStartUtc.HasValue, editor));
    }

    /// <summary>
    /// Cancels an event, or occurrences of a repeating event.
    /// </summary>
    /// <param name="id">The event's id.</param>
    /// <param name="occurrence">When the occurrence was due to start, for an occurrence of a repeating event.</param>
    /// <param name="scope">Which occurrences to cancel.</param>
    /// <returns>A redirect to the calendar.</returns>
    [HttpPost]
    [Admin("calendar/events/cancel/{id}", "CalendarEventsCancel")]
    public async Task<IActionResult> Cancel(string id, string occurrence, string scope)
    {
        var (calendarEvent, calendar) = await LoadAsync(id);

        if (calendarEvent is null)
        {
            return NotFound();
        }

        if (!await _editor.CanChangeAsync(User, calendarEvent, calendar))
        {
            return Forbid();
        }

        var options = new CalendarChangeOptions { Actor = CalendarUser.GetId(User) };
        var originalStartUtc = ParseOccurrence(calendarEvent, occurrence);
        var result = originalStartUtc.HasValue
            ? await _eventManager.CancelOccurrenceAsync(calendarEvent, originalStartUtc.Value, Enum.TryParse<CalendarEditScope>(scope, out var editScope) ? editScope : CalendarEditScope.ThisOccurrence, options)
            : await _eventManager.CancelAsync(calendarEvent, options);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The event has been cancelled."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The event could not be cancelled: {0}", string.Join(" ", result.Errors.Select(error => error.Message))]);
        }

        return RedirectToCalendar(calendarEvent);
    }

    /// <summary>
    /// Deletes an event.
    /// </summary>
    /// <param name="id">The event's id.</param>
    /// <returns>A redirect to the calendar.</returns>
    [HttpPost]
    [Admin("calendar/events/delete/{id}", "CalendarEventsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var (calendarEvent, calendar) = await LoadAsync(id);

        if (calendarEvent is null)
        {
            return NotFound();
        }

        if (!await _editor.CanChangeAsync(User, calendarEvent, calendar))
        {
            return Forbid();
        }

        await _eventManager.DeleteAsync(calendarEvent, new CalendarChangeOptions { Actor = CalendarUser.GetId(User) });
        await _notifier.SuccessAsync(H["The event has been deleted."]);

        return RedirectToCalendar(calendarEvent);
    }

    /// <summary>
    /// Confirms an event that waits for approval, such as requested time off.
    /// </summary>
    /// <param name="id">The event's id.</param>
    /// <returns>A redirect to the calendar.</returns>
    [HttpPost]
    [Admin("calendar/events/confirm/{id}", "CalendarEventsConfirm")]
    public async Task<IActionResult> Confirm(string id)
    {
        var (calendarEvent, calendar) = await LoadAsync(id);

        if (calendarEvent is null)
        {
            return NotFound();
        }

        if (await _accessService.GetAccessLevelAsync(User, calendar) < CalendarAccessLevel.Manage)
        {
            return Forbid();
        }

        var result = await _eventManager.UpdateAsync(calendarEvent, target => target.Status = CalendarEventStatus.Confirmed, new CalendarChangeOptions { Actor = CalendarUser.GetId(User) });

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The event has been confirmed."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The event could not be confirmed: {0}", string.Join(" ", result.Errors.Select(error => error.Message))]);
        }

        return RedirectToCalendar(calendarEvent);
    }

    /// <summary>
    /// Records the viewer's answer to an invitation.
    /// </summary>
    /// <param name="id">The event's id.</param>
    /// <param name="response">The answer: Accepted, Tentative or Declined.</param>
    /// <returns>A redirect to the calendar.</returns>
    [HttpPost]
    [Admin("calendar/events/respond/{id}", "CalendarEventsRespond")]
    public async Task<IActionResult> Respond(string id, string response)
    {
        var (calendarEvent, _) = await LoadAsync(id);
        var userId = CalendarUser.GetId(User);

        if (calendarEvent is null || string.IsNullOrEmpty(userId) || !Enum.TryParse<CalendarAttendeeResponse>(response, out var answer))
        {
            return NotFound();
        }

        var result = await _eventManager.RespondAsync(calendarEvent, "user:" + userId, answer, new CalendarChangeOptions { Actor = userId });

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Your answer has been saved."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Your answer could not be saved: {0}", string.Join(" ", result.Errors.Select(error => error.Message))]);
        }

        return RedirectToCalendar(calendarEvent);
    }

    private async Task<(CalendarEvent Event, Calendar Calendar)> LoadAsync(string id)
    {
        var calendarEvent = await _eventManager.FindByIdAsync(id);

        if (calendarEvent is null)
        {
            return (null, null);
        }

        var calendar = await _calendarService.FindByIdAsync(calendarEvent.CalendarId);

        return calendar is null ? (null, null) : (calendarEvent, calendar);
    }

    private async Task<CalendarEvent> NewEventAsync(Calendar calendar, string eventType, string start, string end, bool allDay)
    {
        DateTime? startUtc = null;

        if (!allDay && CalendarAdminEndpoints.TryParseInstant(start, out var parsedStart))
        {
            startUtc = parsedStart;
        }

        var calendarEvent = await _eventManager.NewAsync(calendar, eventType, startUtc);

        if (allDay && DateTime.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
        {
            calendarEvent.AllDay = true;
            calendarEvent.Start = startDate;
            calendarEvent.End = DateTime.TryParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate) && endDate > startDate
                ? endDate
                : startDate.AddDays(1);
        }
        else if (!allDay && startUtc.HasValue && CalendarAdminEndpoints.TryParseInstant(end, out var parsedEnd) && parsedEnd > startUtc.Value)
        {
            calendarEvent.AllDay = false;
            calendarEvent.End = CalendarTime.ToLocal(parsedEnd, calendarEvent.TimeZoneId);
        }

        return calendarEvent;
    }

    private async Task<EditCalendarEventViewModel> BuildEditModelAsync(CalendarEvent calendarEvent, Calendar calendar, string occurrence, bool isOccurrence, dynamic editor)
    {
        var userId = CalendarUser.GetId(User);
        var mine = string.IsNullOrEmpty(userId) ? null : calendarEvent.Attendees.FirstOrDefault(attendee => attendee.UserId == userId);

        return new EditCalendarEventViewModel
        {
            Id = calendarEvent.ItemId,
            CalendarId = calendar.ItemId,
            CalendarName = calendar.Name,
            Occurrence = isOccurrence ? occurrence : null,
            IsOccurrence = isOccurrence,
            Title = calendarEvent.Title,
            CanConfirm = calendarEvent.Status == CalendarEventStatus.Tentative && await _accessService.GetAccessLevelAsync(User, calendar) >= CalendarAccessLevel.Manage,
            MyResponse = mine?.Response.ToString(),
            Source = calendarEvent.Source,
            Editor = editor,
        };
    }

    private RedirectToActionResult RedirectToCalendar(CalendarEvent calendarEvent)
    {
        return RedirectToAction("Index", "Calendar", new
        {
            area = CalendarsConstants.Features.Calendars,
            date = calendarEvent.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        });
    }

    private static DateTime? ParseOccurrence(CalendarEvent calendarEvent, string occurrence)
    {
        if (string.IsNullOrEmpty(occurrence) || calendarEvent.SeriesId is not null || calendarEvent.Recurrence is null)
        {
            return null;
        }

        return CalendarAdminEndpoints.TryParseInstant(occurrence, out var utc) ? utc : null;
    }

    // An occurrence-shaped copy of a series for the editor: the occurrence's times, marked as part of the series.
    private static CalendarEvent CreateOccurrenceCopy(CalendarEvent series, DateTime originalStartUtc)
    {
        var copy = CloneForEditing(series);
        var localStart = CalendarTime.ToLocal(originalStartUtc, series.TimeZoneId);
        var length = series.End - series.Start;

        copy.Start = series.AllDay ? localStart.Date : localStart;
        copy.End = copy.Start + length;
        copy.SeriesId = series.ItemId;
        copy.OriginalStartUtc = originalStartUtc;

        return copy;
    }

    private static CalendarEvent CloneForEditing(CalendarEvent calendarEvent)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(calendarEvent);

        return System.Text.Json.JsonSerializer.Deserialize<CalendarEvent>(json);
    }
}
