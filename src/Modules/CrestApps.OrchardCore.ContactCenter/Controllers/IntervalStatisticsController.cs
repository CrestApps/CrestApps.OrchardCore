using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// The interval statistics status page: how far they are computed, today's totals so far, and a rebuild of a range of
/// days.
/// </summary>
public sealed class IntervalStatisticsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IntervalStatisticsRoller _roller;
    private readonly IIntervalStatisticsReader _reader;
    private readonly IntervalStatisticsOptions _options;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalStatisticsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="roller">The roller.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="options">The options.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public IntervalStatisticsController(
        IAuthorizationService authorizationService,
        IntervalStatisticsRoller roller,
        IIntervalStatisticsReader reader,
        IOptions<IntervalStatisticsOptions> options,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<IntervalStatisticsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _roller = roller;
        _reader = reader;
        _options = options.Value;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows the status page.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("contact-center/interval-statistics", "ContactCenterIntervalStatistics")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ViewReports))
        {
            return Forbid();
        }

        var status = await IntervalStatisticsStatus.ReadAsync(_roller, _reader, _clock.UtcNow, HttpContext.RequestAborted);
        var today = status.TodayUtc;

        var model = new IntervalStatisticsViewModel
        {
            State = status.State,
            DaysComputed = status.DaysComputed,
            DaysFinal = status.DaysFinal,
            Today = status.Today,
            TodayTotal = status.TodayTotal,
            QueuesToday = status.QueuesToday,
            RetentionDays = _options.RetentionDays,
            BackfillDays = _options.BackfillDays,
            CanRebuild = await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageContactCenter),
            Stalled = status.Stalled,
            RebuildFrom = today.AddDays(-7),
            RebuildTo = today,
        };

        return View(model);
    }

    /// <summary>
    /// Asks for a range of days to be computed again.
    /// </summary>
    /// <param name="model">The range.</param>
    /// <returns>The status page.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("contact-center/interval-statistics/rebuild", "ContactCenterIntervalStatisticsRebuild")]
    public async Task<IActionResult> Rebuild(IntervalStatisticsViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageContactCenter))
        {
            return Forbid();
        }

        if (model.RebuildTo < model.RebuildFrom)
        {
            await _notifier.ErrorAsync(H["The last day must not be before the first day."]);

            return RedirectToAction(nameof(Index));
        }

        try
        {
            var state = await _roller.RequestRebuildAsync(
                DateTime.SpecifyKind(model.RebuildFrom, DateTimeKind.Utc),
                DateTime.SpecifyKind(model.RebuildTo, DateTimeKind.Utc),
                User.Identity?.Name,
                HttpContext.RequestAborted);

            var count = (int)(state.RebuildToUtc.Value - state.RebuildFromUtc.Value).TotalDays + 1;

            await _notifier.SuccessAsync(H["The statistics of {0} day(s) will be computed again, {1} days every five minutes.", count, _options.MaxDaysPerRun]);
        }
        catch (ConcurrencyException)
        {
            await _notifier.WarningAsync(H["The statistics were being updated. Try again."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
