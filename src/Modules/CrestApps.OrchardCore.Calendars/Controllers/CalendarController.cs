using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Endpoints;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Controllers;

/// <summary>
/// The calendar view: the viewer's calendars in a day, week, month or agenda view.
/// </summary>
public sealed class CalendarController : Controller
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAuthorizationService _authorizationService;
    private readonly ICalendarAccessService _accessService;
    private readonly ICalendarService _calendarService;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="accessService">The calendar access service.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <param name="localizerFactory">The localizer factory.</param>
    /// <param name="clock">The clock.</param>
    public CalendarController(
        IAuthorizationService authorizationService,
        ICalendarAccessService accessService,
        ICalendarService calendarService,
        ICalendarTypeManager typeManager,
        IStringLocalizerFactory localizerFactory,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _accessService = accessService;
        _calendarService = calendarService;
        _typeManager = typeManager;
        _localizerFactory = localizerFactory;
        _clock = clock;
    }

    /// <summary>
    /// Shows the calendar view.
    /// </summary>
    /// <param name="date">The day to show first, as yyyy-MM-dd.</param>
    /// <param name="view">The view to show first: day, week, month or agenda.</param>
    /// <param name="calendars">The calendars to show, comma separated; the viewer's own calendars when empty.</param>
    /// <param name="schedule">Whose schedule to show instead of calendars: <c>everyone</c>, <c>people</c>, or user ids separated by commas.</param>
    /// <param name="people">The users whose schedules to show when <paramref name="schedule"/> is <c>people</c>.</param>
    /// <param name="userLookup">The user lookup.</param>
    /// <returns>The page.</returns>
    [Admin("calendar", "CalendarView")]
    public async Task<IActionResult> Index(string date, string view, string calendars, string schedule, string[] people, [FromServices] CalendarUserLookup userLookup)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CalendarPermissions.AccessCalendars))
        {
            return Forbid();
        }

        // The schedule form sends "people" with the users picked; a link can name the users directly, separated by commas.
        var everyone = string.Equals(schedule, CalendarAdminEndpoints.EveryoneSchedule, StringComparison.OrdinalIgnoreCase);
        var userIds = everyone
            ? []
            : CalendarAdminEndpoints.ParseUserIds(string.Equals(schedule, "people", StringComparison.OrdinalIgnoreCase) ? string.Join(',', people ?? []) : schedule);

        foreach (var shown in everyone ? [CalendarSchedule.Everyone] : userIds.Select(id => new CalendarSchedule(id)))
        {
            if (!await _authorizationService.AuthorizeAsync(User, CalendarPermissions.ListCalendarEvents, shown))
            {
                return Forbid();
            }
        }

        var userId = CalendarUser.GetId(User);
        Calendar personal = null;

        if (!string.IsNullOrEmpty(userId) && _typeManager.FindType(CalendarsConstants.Types.Personal) is not null)
        {
            personal = await _calendarService.GetOrCreatePersonalCalendarAsync(userId, User.Identity?.Name);
        }

        var accessible = await _accessService.ListAccessibleAsync(User, CalendarAccessLevel.FreeBusy);
        var requested = (calendars ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var model = new CalendarViewViewModel
        {
            CanCreateCalendars = await _authorizationService.AuthorizeAsync(User, CalendarPermissions.CreateCalendars),
            CanViewSchedules = await _authorizationService.AuthorizeAsync(User, CalendarPermissions.ListCalendarEvents, CalendarSchedule.Everyone),
        };

        if (everyone)
        {
            model.ScheduleMode = CalendarAdminEndpoints.EveryoneSchedule;
        }
        else if (userIds.Count > 0)
        {
            var picked = await userLookup.GetAsync(userIds);

            model.ScheduleMode = "people";
            model.ScheduleNames = userIds.Select(id => picked.FirstOrDefault(item => item.Value == id)?.Text ?? id).ToList();
            model.SchedulePeopleJson = JsonSerializer.Serialize(picked.Select(item => new { value = item.Value, text = item.Text }), _jsonOptions);
        }

        foreach (var access in accessible.Where(access => access.Calendar.Enabled || access.Level >= CalendarAccessLevel.Manage))
        {
            var type = _typeManager.FindType(access.Calendar.Type);
            var selected = requested.Count > 0
                ? requested.Contains(access.Calendar.ItemId)
                : access.Calendar.ItemId == personal?.ItemId || access.Level >= CalendarAccessLevel.Write;

            model.Calendars.Add(new CalendarViewCalendar
            {
                Id = access.Calendar.ItemId,
                Name = access.Calendar.Name,
                Color = access.Calendar.Color ?? type?.DefaultColor ?? "#1971c2",
                TypeName = type is null ? access.Calendar.Type : _localizerFactory.Localize(type.DisplayName).Value,
                TypeIcon = type?.Icon ?? "fa-solid fa-calendar",
                Writable = access.Calendar.Enabled && access.Level >= CalendarAccessLevel.Write,
                Selected = selected,
            });
        }

        model.ConfigurationJson = JsonSerializer.Serialize(new
        {
            occurrencesUrl = Url.RouteUrl("CalendarsOccurrences"),
            moveUrl = Url.RouteUrl("CalendarsMoveOccurrence"),
            chooseUrl = Url.Action("Choose", "CalendarEvents", new { area = CalendarsConstants.Features.Calendars }),
            createUrl = Url.Action("Create", "CalendarEvents", new { area = CalendarsConstants.Features.Calendars }),
            initialDate = DateTime.TryParse(date, out var initial) ? initial.ToString("yyyy-MM-dd") : _clock.UtcNow.ToString("yyyy-MM-dd"),
            initialView = view switch
            {
                "day" => "timeGridDay",
                "month" => "dayGridMonth",
                "agenda" => "listWeek",
                _ => "timeGridWeek",
            },
            locale = System.Globalization.CultureInfo.CurrentUICulture.Name,
            schedule = everyone ? CalendarAdminEndpoints.EveryoneSchedule : userIds.Count > 0 ? string.Join(',', userIds) : null,
            writableCalendars = model.Calendars.Where(calendar => calendar.Writable).Select(calendar => calendar.Id),
        }, _jsonOptions);

        return View(model);
    }
}
