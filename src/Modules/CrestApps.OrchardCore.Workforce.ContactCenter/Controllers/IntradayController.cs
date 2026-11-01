using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// The day as it happens for a planning group, the reforecast, the quietest times and the offers.
/// </summary>
public sealed class IntradayController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IntradayService _intradayService;
    private readonly PlanningGroupManager _groupManager;
    private readonly IWorkerManager _workerManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntradayController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="intradayService">The intraday service.</param>
    /// <param name="groupManager">The planning group manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public IntradayController(
        IAuthorizationService authorizationService,
        IntradayService intradayService,
        PlanningGroupManager groupManager,
        IWorkerManager workerManager,
        ISession session,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<IntradayController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _intradayService = intradayService;
        _groupManager = groupManager;
        _workerManager = workerManager;
        _session = session;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows a planning group's day.
    /// </summary>
    /// <param name="model">The group, the day and the activity finder's people and minutes.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/intraday", "WorkforceIntraday")]
    public async Task<IActionResult> Index(IntradayViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var groups = (await _groupManager.GetAllAsync(HttpContext.RequestAborted)).OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var group = groups.FirstOrDefault(item => item.ItemId == model.PlanningGroupId) ?? groups.FirstOrDefault();

        model.PlanningGroupOptions = [.. groups.Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == group?.ItemId))];
        model.Group = group;

        if (group is null)
        {
            return View(model);
        }

        model.PlanningGroupId = group.ItemId;
        model.TimeZoneId = await _intradayService.TimeZoneOfAsync(group, HttpContext.RequestAborted);

        if (model.Date == default)
        {
            model.Date = DateOnly.FromDateTime(CalendarTime.ToLocal(_clock.UtcNow, model.TimeZoneId));
        }

        model.Rows = await _intradayService.DayAsync(group, model.Date, HttpContext.RequestAborted);
        model.Factor = IntradayCalculator.ReforecastFactor(model.Rows);
        model.Windows = IntradayCalculator.FindWindows(model.Rows, Math.Max(1, model.People), Math.Max(15, model.Minutes), _clock.UtcNow);

        var dayStart = CalendarTime.ToUtc(model.Date.ToDateTime(TimeOnly.MinValue), model.TimeZoneId);

        model.Offers = await _intradayService.ListAsync(group.ItemId, dayStart, dayStart.AddDays(1), HttpContext.RequestAborted);

        foreach (var workerId in model.Offers.SelectMany(offer => offer.Claims).Select(claim => claim.WorkerId).Distinct(StringComparer.Ordinal))
        {
            model.WorkerNames[workerId] = (await _workerManager.FindByIdAsync(workerId, HttpContext.RequestAborted))?.DisplayName ?? workerId;
        }

        return View(model);
    }

    /// <summary>
    /// Scales the rest of the day by how far the work so far is from the forecast.
    /// </summary>
    /// <param name="planningGroupId">The planning group.</param>
    /// <param name="date">The day.</param>
    /// <returns>The day.</returns>
    [HttpPost]
    [Admin("workforce/intraday/reforecast", "WorkforceIntradayReforecast")]
    public async Task<IActionResult> Reforecast(string planningGroupId, DateOnly date)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var group = string.IsNullOrEmpty(planningGroupId) ? null : await _groupManager.FindByIdAsync(planningGroupId, HttpContext.RequestAborted);

        if (group is null)
        {
            return NotFound();
        }

        var (factor, intervals) = await _intradayService.ReforecastAsync(group, date, User.Identity?.Name, HttpContext.RequestAborted);

        if (intervals == 0)
        {
            await _notifier.InformationAsync(H["The work so far is close to the forecast, or the rest of the day has none; nothing was changed."]);
        }
        else
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The rest of the day was scaled by {0}% and staffed again; the team's coverage follows.", ((factor - 1) * 100).ToString("+0.#;-0.#", CultureInfo.CurrentCulture)]);
        }

        return RedirectToAction(nameof(Index), new { planningGroupId, date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
    }

    /// <summary>
    /// Offers voluntary overtime or time off to the group's team.
    /// </summary>
    /// <param name="offer">The offer.</param>
    /// <returns>The day.</returns>
    [HttpPost]
    [Admin("workforce/intraday/offer", "WorkforceIntradayOffer")]
    public async Task<IActionResult> Offer(IntradayOfferEditViewModel offer)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var group = string.IsNullOrEmpty(offer.PlanningGroupId) ? null : await _groupManager.FindByIdAsync(offer.PlanningGroupId, HttpContext.RequestAborted);

        if (group is null)
        {
            return NotFound();
        }

        var zone = await _intradayService.TimeZoneOfAsync(group, HttpContext.RequestAborted);
        var end = offer.End <= offer.Start ? offer.Date.AddDays(1).ToDateTime(offer.End) : offer.Date.ToDateTime(offer.End);
        var errors = await _intradayService.OfferAsync(new IntradayOffer
        {
            PlanningGroupId = group.ItemId,
            Kind = offer.Kind,
            StartUtc = CalendarTime.ToUtc(offer.Date.ToDateTime(offer.Start), zone),
            EndUtc = CalendarTime.ToUtc(end, zone),
            Places = offer.Places,
            Note = string.IsNullOrWhiteSpace(offer.Note) ? null : offer.Note.Trim(),
            CreatedBy = User.Identity?.Name,
        }, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", errors)]);
        }
        else
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The offer is in the team's My Work. Workers take it first come, first served."]);
        }

        return RedirectToAction(nameof(Index), new { planningGroupId = group.ItemId, date = offer.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
    }

    /// <summary>
    /// Closes an offer.
    /// </summary>
    /// <param name="id">The offer.</param>
    /// <returns>The day.</returns>
    [HttpPost]
    [Admin("workforce/intraday/offer/{id}/close", "WorkforceIntradayOfferClose")]
    public async Task<IActionResult> Close(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var offer = await _intradayService.FindAsync(id, HttpContext.RequestAborted);

        if (offer is null)
        {
            return NotFound();
        }

        await _intradayService.CloseAsync(offer, HttpContext.RequestAborted);
        await _session.SaveChangesAsync(HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The offer is closed."]);

        var group = await _groupManager.FindByIdAsync(offer.PlanningGroupId, HttpContext.RequestAborted);
        var zone = await _intradayService.TimeZoneOfAsync(group, HttpContext.RequestAborted);
        var date = DateOnly.FromDateTime(CalendarTime.ToLocal(offer.StartUtc, zone));

        return RedirectToAction(nameof(Index), new { planningGroupId = offer.PlanningGroupId, date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
    }
}
