using System.Globalization;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.TimeTracker.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.TimeTracker.Controllers;

/// <summary>
/// The time report: a row for each person, or each person and day, and a column for each category.
/// </summary>
public sealed class TimeReportController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITimeTrackerService _tracker;
    private readonly TimeCategoryStore _categories;
    private readonly TimeTrackerTimeZone _timeZone;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeReportController"/> class.
    /// </summary>
    public TimeReportController(IAuthorizationService authorizationService, ITimeTrackerService tracker, TimeCategoryStore categories, TimeTrackerTimeZone timeZone, IClock clock)
    {
        _authorizationService = authorizationService;
        _tracker = tracker;
        _categories = categories;
        _timeZone = timeZone;
        _clock = clock;
    }

    /// <summary>
    /// Shows the report for a range of days.
    /// </summary>
    [Admin("time/report", "TimeTrackerReport")]
    public async Task<IActionResult> Index(TimeReportViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ViewTimeEntries))
        {
            return Forbid();
        }

        var zone = await _timeZone.GetAsync();
        var today = TimeTrackerTimeZone.ToLocal(_clock.UtcNow, zone).Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var from = DateTime.TryParseExact(form.From, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ? first : monday;
        var to = DateTime.TryParseExact(form.To, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) && last >= from ? last : from.AddDays(6);
        var rows = await _tracker.GetTotalsAsync(new TimeEntryQuery { FromUtc = TimeTrackerTimeZone.ToUtc(from, zone), ToUtc = TimeTrackerTimeZone.ToUtc(to.AddDays(1), zone) }, zone, form.ByDay, HttpContext.RequestAborted);

        form.From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        form.To = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        form.TimeZoneId = zone;
        form.Categories = [.. (await _categories.GetAllAsync(HttpContext.RequestAborted)).Where(category => category.Enabled).OrderBy(category => category.Position).ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)];
        form.Rows = [.. rows.Where(row => string.IsNullOrWhiteSpace(form.Q) || (row.UserName ?? string.Empty).Contains(form.Q.Trim(), StringComparison.OrdinalIgnoreCase))];

        return View(form);
    }
}
