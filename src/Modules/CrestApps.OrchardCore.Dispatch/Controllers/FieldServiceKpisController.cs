using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Reports;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Dispatch.Controllers;

/// <summary>
/// The field service KPIs under Reports: first-time fix, on-time arrival, travel against time on site, jobs per
/// technician a day, return visits, service level compliance and the time to repair.
/// </summary>
public sealed class FieldServiceKpisController : Controller
{
    private const int MaxDays = 366;

    private readonly IAuthorizationService _authorizationService;
    private readonly FieldServiceKpiService _kpis;
    private readonly DispatchTimeZone _timeZone;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldServiceKpisController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="kpis">The KPI service.</param>
    /// <param name="timeZone">The dispatch time zone.</param>
    /// <param name="clock">The clock.</param>
    public FieldServiceKpisController(IAuthorizationService authorizationService, FieldServiceKpiService kpis, DispatchTimeZone timeZone, IClock clock)
    {
        _authorizationService = authorizationService;
        _kpis = kpis;
        _timeZone = timeZone;
        _clock = clock;
    }

    /// <summary>
    /// Shows the KPIs for a range of days, the last 30 by default.
    /// </summary>
    /// <param name="form">The range.</param>
    /// <returns>The page.</returns>
    [Admin("dispatch/kpis", "FieldServiceKpis")]
    public async Task<IActionResult> Index(FieldServiceKpisViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ViewDispatch))
        {
            return Forbid();
        }

        var zone = await _timeZone.GetAsync();
        var today = CalendarTime.ToLocal(_clock.UtcNow, zone).Date;
        var to = DateTime.TryParseExact(form.To, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) ? last : today;
        var from = DateTime.TryParseExact(form.From, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) && first <= to ? first : to.AddDays(-29);

        if ((to - from).TotalDays >= MaxDays)
        {
            from = to.AddDays(-(MaxDays - 1));
        }

        form.From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        form.To = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        form.TimeZoneId = zone;
        form.RepeatDays = Math.Clamp(form.RepeatDays ?? Reports.FieldServiceKpiService.DefaultRepeatDays, 1, 365);
        form.Result = await _kpis.GetAsync(CalendarTime.ToUtc(from, zone), CalendarTime.ToUtc(to.AddDays(1), zone), zone, form.RepeatDays.Value, HttpContext.RequestAborted);

        return View(form);
    }
}
