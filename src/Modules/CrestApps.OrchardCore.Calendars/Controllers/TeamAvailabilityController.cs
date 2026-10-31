using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.Modules;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.Controllers;

/// <summary>
/// Shows when a team's members are busy on a day, and when they are all free.
/// </summary>
[Feature(CalendarsConstants.Features.Teams)]
public sealed class TeamAvailabilityController : Controller
{
    private const int MaxMembers = 50;

    private readonly IAuthorizationService _authorizationService;
    private readonly ICalendarAccessService _accessService;
    private readonly ICalendarService _calendarService;
    private readonly ICalendarAvailabilityService _availabilityService;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamAvailabilityController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="accessService">The calendar access service.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="availabilityService">The free/busy service.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    public TeamAvailabilityController(
        IAuthorizationService authorizationService,
        ICalendarAccessService accessService,
        ICalendarService calendarService,
        ICalendarAvailabilityService availabilityService,
        ISession session,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _accessService = accessService;
        _calendarService = calendarService;
        _availabilityService = availabilityService;
        _session = session;
        _clock = clock;
    }

    /// <summary>
    /// Shows a team's day.
    /// </summary>
    /// <param name="calendarId">The team calendar.</param>
    /// <param name="date">The day, as yyyy-MM-dd.</param>
    /// <param name="minutes">How long a meeting must be.</param>
    /// <returns>The page.</returns>
    [Admin("calendar/availability", "CalendarTeamAvailability")]
    public async Task<IActionResult> Index(string calendarId, string date, int minutes = 30)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CalendarPermissions.AccessCalendars))
        {
            return Forbid();
        }

        var teams = await _accessService.ListAccessibleAsync(User, CalendarAccessLevel.Read, CalendarsConstants.Types.Team);
        var selected = teams.FirstOrDefault(access => access.Calendar.ItemId == calendarId)?.Calendar ?? (teams.Count > 0 ? teams[0].Calendar : null);
        var model = new TeamAvailabilityViewModel
        {
            CalendarId = selected?.ItemId,
            Minutes = Math.Clamp(minutes, 15, 480),
            CalendarOptions = teams.Select(access => new SelectListItem(access.Calendar.Name, access.Calendar.ItemId, access.Calendar.ItemId == selected?.ItemId)).ToList(),
        };

        if (selected is null)
        {
            model.Date = _clock.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            return View(model);
        }

        model.TimeZoneId = CalendarTime.NormalizeZoneId(selected.TimeZoneId) ?? "UTC";

        var today = CalendarTime.ToLocal(_clock.UtcNow, model.TimeZoneId).Date;
        var day = DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : today;

        model.Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var fromUtc = CalendarTime.ToUtc(day.AddHours(model.FirstHour), model.TimeZoneId);
        var toUtc = CalendarTime.ToUtc(day.AddHours(model.LastHour), model.TimeZoneId);
        var role = selected.OwnerId?.ToUpperInvariant();
        var members = string.IsNullOrEmpty(role)
            ? []
            : await _session.Query<User, UserByRoleNameIndex>(index => index.RoleName == role).Take(MaxMembers).ListAsync();

        var busy = await _availabilityService.GetBusyAsync(new CalendarBusyQuery
        {
            UserIds = members.Select(member => member.UserId).ToList(),
            FromUtc = fromUtc,
            ToUtc = toUtc,
        });

        var slots = (model.LastHour - model.FirstHour) * 2;

        foreach (var member in members.OrderBy(member => member.UserName, StringComparer.OrdinalIgnoreCase))
        {
            var row = new TeamAvailabilityRow { Name = member.UserName };
            var memberBusy = busy.Where(interval => interval.UserId == member.UserId).ToList();

            for (var slot = 0; slot < slots; slot++)
            {
                var slotStart = fromUtc.AddMinutes(slot * 30);
                var slotEnd = slotStart.AddMinutes(30);
                var overlapping = memberBusy.Where(interval => interval.StartUtc < slotEnd && interval.EndUtc > slotStart).ToList();

                row.Slots.Add(overlapping.Count == 0 ? 0 : overlapping.All(interval => interval.Tentative) ? 1 : 2);
            }

            model.Rows.Add(row);
        }

        var free = await _availabilityService.FindFreeWindowsAsync(new CalendarBusyQuery
        {
            UserIds = members.Select(member => member.UserId).ToList(),
            FromUtc = fromUtc,
            ToUtc = toUtc,
            IncludeTentative = false,
        }, TimeSpan.FromMinutes(model.Minutes), maxResults: 10);

        foreach (var window in free)
        {
            model.FreeTimes.Add(new KeyValuePair<DateTime, DateTime>(CalendarTime.ToLocal(window.StartUtc, model.TimeZoneId), CalendarTime.ToLocal(window.EndUtc, model.TimeZoneId)));
        }

        return View(model);
    }
}
