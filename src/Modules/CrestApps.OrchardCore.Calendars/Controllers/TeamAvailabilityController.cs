using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Controllers;

/// <summary>
/// The Team Availability page: for a team calendar and a day, when each member of the team is busy, and the first times
/// everyone is free.
/// </summary>
[Feature(CalendarsConstants.Features.Teams)]
public sealed class TeamAvailabilityController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICalendarAccessService _accessService;
    private readonly TeamAvailabilityService _teamAvailabilityService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamAvailabilityController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="accessService">The calendar access service.</param>
    /// <param name="teamAvailabilityService">The team availability service.</param>
    /// <param name="clock">The clock.</param>
    public TeamAvailabilityController(
        IAuthorizationService authorizationService,
        ICalendarAccessService accessService,
        TeamAvailabilityService teamAvailabilityService,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _accessService = accessService;
        _teamAvailabilityService = teamAvailabilityService;
        _clock = clock;
    }

    /// <summary>
    /// Shows the team availability of a day.
    /// </summary>
    /// <param name="calendarId">The team calendar's id.</param>
    /// <param name="date">The day (yyyy-MM-dd).</param>
    /// <param name="minutes">The length of the meeting to find free times for.</param>
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

        var availability = await _teamAvailabilityService.GetAsync(
            selected,
            DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null,
            model.Minutes,
            model.FirstHour,
            model.LastHour,
            HttpContext.RequestAborted);

        model.TimeZoneId = availability.TimeZoneId;
        model.Date = availability.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        foreach (var member in availability.Members)
        {
            var row = new TeamAvailabilityRow { Name = member.UserName };

            foreach (var slot in member.Slots)
            {
                row.Slots.Add(slot);
            }

            model.Rows.Add(row);
        }

        foreach (var window in availability.FreeTimes)
        {
            model.FreeTimes.Add(new KeyValuePair<DateTime, DateTime>(CalendarTime.ToLocal(window.StartUtc, model.TimeZoneId), CalendarTime.ToLocal(window.EndUtc, model.TimeZoneId)));
        }

        return View(model);
    }
}
