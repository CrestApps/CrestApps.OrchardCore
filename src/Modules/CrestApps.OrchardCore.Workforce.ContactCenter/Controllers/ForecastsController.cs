using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// Forecasts: making one from history, reading it day by day, adjusting it and making it the team's coverage.
/// </summary>
public sealed class ForecastsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ForecastService _forecastService;
    private readonly PlanningGroupManager _groupManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="forecastService">The forecast service.</param>
    /// <param name="groupManager">The planning group manager.</param>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public ForecastsController(
        IAuthorizationService authorizationService,
        ForecastService forecastService,
        PlanningGroupManager groupManager,
        ISession session,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<ForecastsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _forecastService = forecastService;
        _groupManager = groupManager;
        _session = session;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the forecasts, newest first.
    /// </summary>
    /// <param name="planningGroupId">The planning group filter.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/forecasts", "WorkforceForecasts")]
    public async Task<IActionResult> Index(string planningGroupId, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var groups = (await _groupManager.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(group => group.ItemId, StringComparer.Ordinal);
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _forecastService.PageAsync(planningGroupId, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        return View(new ForecastingListViewModel
        {
            PlanningGroupId = planningGroupId,
            PlanningGroupOptions = [.. groups.Values.OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase).Select(group => new SelectListItem(group.Name, group.ItemId, group.ItemId == planningGroupId))],
            List = new AdminListContext<ForecastRow>
            {
                Source = [.. items.Select(item => new ForecastRow { Forecast = item, PlanningGroupName = groups.TryGetValue(item.PlanningGroupId, out var group) ? group.Name : item.PlanningGroupId })],
                Pager = await shapeFactory.PagerAsync(pager, count),
            },
        });
    }

    /// <summary>
    /// Shows the form that makes a forecast.
    /// </summary>
    /// <param name="planningGroupId">The planning group, when known.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/forecasts/create", "WorkforceForecastsCreate")]
    public async Task<IActionResult> Create(string planningGroupId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var model = new ForecastCreateViewModel
        {
            PlanningGroupId = planningGroupId,
            FromDate = today.AddDays(((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7 == 0 ? 7 : ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7),
            Model = ForecastModelKind.Auto,
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Makes a forecast.
    /// </summary>
    /// <param name="form">The form. It isn't called model, which its Model property would be bound under.</param>
    /// <returns>The forecast, or the form with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("workforce/forecasts/create", "WorkforceForecastsCreate")]
    public async Task<IActionResult> CreatePost(ForecastCreateViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        if (form.Weeks is < 1 or > 13)
        {
            ModelState.AddModelError(nameof(form.Weeks), H["Forecast 1 to 13 weeks."].Value);
        }

        if (ModelState.IsValid)
        {
            var created = await _forecastService.CreateAsync(form.PlanningGroupId, form.FromDate, form.FromDate.AddDays((form.Weeks * 7) - 1), form.Model, User.Identity?.Name, HttpContext.RequestAborted);

            if (created.Succeeded)
            {
                await _session.SaveChangesAsync(HttpContext.RequestAborted);
                await _notifier.SuccessAsync(H["The forecast was made as a draft. Check it, adjust it if needed, then make it the team's coverage."]);

                return RedirectToAction(nameof(Details), new { id = created.Value.ItemId });
            }

            foreach (var error in created.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        await PopulateAsync(form);

        return View(form);
    }

    /// <summary>
    /// Shows a forecast: its days, and one day's intervals.
    /// </summary>
    /// <param name="id">The forecast.</param>
    /// <param name="day">The day to show; the first busy one when none is given.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/forecasts/{id}", "WorkforceForecastsDetails")]
    public async Task<IActionResult> Details(string id, DateOnly? day)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var forecast = await _forecastService.FindAsync(id, HttpContext.RequestAborted);

        if (forecast is null)
        {
            return NotFound();
        }

        var shown = day ?? forecast.Intervals.Select(interval => interval.Date).DefaultIfEmpty(forecast.FromDate).Min();

        return View(new ForecastDetailsViewModel
        {
            Forecast = forecast,
            Group = await _groupManager.FindByIdAsync(forecast.PlanningGroupId, HttpContext.RequestAborted),
            Day = shown,
            AdjustFrom = shown,
            AdjustTo = shown,
        });
    }

    /// <summary>
    /// Adjusts a forecast's volume over some days.
    /// </summary>
    /// <param name="id">The forecast.</param>
    /// <param name="model">The adjustment.</param>
    /// <returns>The forecast.</returns>
    [HttpPost]
    [Admin("workforce/forecasts/{id}/adjust", "WorkforceForecastsAdjust")]
    public async Task<IActionResult> Adjust(string id, ForecastDetailsViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var forecast = await _forecastService.FindAsync(id, HttpContext.RequestAborted);

        if (forecast is null)
        {
            return NotFound();
        }

        var errors = await _forecastService.AdjustAsync(forecast, new ForecastAdjustment
        {
            FromDate = model.AdjustFrom,
            ToDate = model.AdjustTo,
            Percent = model.AdjustPercent,
            Note = string.IsNullOrWhiteSpace(model.AdjustNote) ? null : model.AdjustNote.Trim(),
            By = User.Identity?.Name,
        }, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", errors)]);
        }
        else
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The forecast was adjusted and staffed again."]);
        }

        return RedirectToAction(nameof(Details), new { id, day = model.AdjustFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
    }

    /// <summary>
    /// Makes a forecast its group's active forecast and its team's coverage.
    /// </summary>
    /// <param name="id">The forecast.</param>
    /// <returns>The forecast.</returns>
    [HttpPost]
    [Admin("workforce/forecasts/{id}/activate", "WorkforceForecastsActivate")]
    public async Task<IActionResult> Activate(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var forecast = await _forecastService.FindAsync(id, HttpContext.RequestAborted);

        if (forecast is null)
        {
            return NotFound();
        }

        var errors = await _forecastService.ActivateAsync(forecast, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", errors)]);
        }
        else
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The forecast is active. Its staffing is now the team's coverage, which the planner and the auto-scheduler use."]);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Deletes a forecast.
    /// </summary>
    /// <param name="id">The forecast.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("workforce/forecasts/{id}/delete", "WorkforceForecastsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var forecast = await _forecastService.FindAsync(id, HttpContext.RequestAborted);

        if (forecast is null)
        {
            return NotFound();
        }

        _forecastService.Remove(forecast);
        await _session.SaveChangesAsync(HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The forecast was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(ForecastCreateViewModel model)
    {
        model.PlanningGroupOptions = (await _groupManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new SelectListItem(group.Name, group.ItemId, group.ItemId == model.PlanningGroupId))
            .ToList();
    }
}
