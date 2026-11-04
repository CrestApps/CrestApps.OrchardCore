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
/// Forecast events: holidays, closures and promotions that change a day's work.
/// </summary>
public sealed class ForecastEventsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ForecastEventManager _manager;
    private readonly PlanningGroupManager _groupManager;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastEventsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The forecast event manager.</param>
    /// <param name="groupManager">The planning group manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public ForecastEventsController(
        IAuthorizationService authorizationService,
        ForecastEventManager manager,
        PlanningGroupManager groupManager,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<ForecastEventsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _groupManager = groupManager;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the events, latest first.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/forecasting/events", "WorkforceForecastEvents")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var items = (await _manager.GetAllAsync(HttpContext.RequestAborted)).OrderByDescending(item => item.StartDate).ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new ForecastingListViewModel
        {
            List = new AdminListContext<ForecastEvent>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The event, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/forecasting/events/edit/{id?}", "WorkforceForecastEventsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _manager.NewAsync() : await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var model = new ForecastEventEditViewModel
        {
            Id = id,
            Name = item.Name,
            Kind = item.Kind,
            StartDate = string.IsNullOrEmpty(id) ? today : item.StartDate,
            EndDate = string.IsNullOrEmpty(id) ? today : item.EndDate,
            ChangePercent = Math.Round((item.VolumeMultiplier - 1) * 100, 1),
            RecursYearly = item.RecursYearly,
            PlanningGroupIds = [.. item.PlanningGroupIds],
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves the editor.
    /// </summary>
    /// <param name="model">The form.</param>
    /// <returns>The list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/forecasting/events/edit/{id?}", "WorkforceForecastEventsEdit")]
    public async Task<IActionResult> EditPost(ForecastEventEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var item = isNew ? await _manager.NewAsync() : await _manager.FindByIdAsync(model.Id);

        if (item is null)
        {
            return NotFound();
        }

        item.Name = model.Name?.Trim();
        item.Kind = model.Kind;
        item.StartDate = model.StartDate;
        item.EndDate = model.EndDate;
        item.VolumeMultiplier = Math.Max(0, 1 + (model.ChangePercent / 100d));
        item.RecursYearly = model.RecursYearly;
        item.PlanningGroupIds = [.. (model.PlanningGroupIds ?? []).Where(groupId => !string.IsNullOrEmpty(groupId)).Distinct(StringComparer.Ordinal)];

        var validation = await _manager.ValidateAsync(item);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);

            return View(model);
        }

        if (isNew)
        {
            await _manager.CreateAsync(item);
        }
        else
        {
            await _manager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The event was saved. Forecasts made from now on use it."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes an event.
    /// </summary>
    /// <param name="id">The event.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("workforce/forecasting/events/delete/{id}", "WorkforceForecastEventsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var item = await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        await _manager.DeleteAsync(item);
        await _notifier.SuccessAsync(H["The event was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(ForecastEventEditViewModel model)
    {
        var chosen = model.PlanningGroupIds.ToHashSet(StringComparer.Ordinal);

        model.PlanningGroupOptions = (await _groupManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new SelectListItem(group.Name, group.ItemId, chosen.Contains(group.ItemId)))
            .ToList();
    }
}
