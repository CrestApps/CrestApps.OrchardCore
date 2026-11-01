using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
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

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// Capacity plans: the agents a planning group needs week by week against those expected, with attrition and hiring.
/// </summary>
public sealed class CapacityPlansController : Controller
{
    private const int BlankHiringRows = 3;

    private readonly IAuthorizationService _authorizationService;
    private readonly CapacityPlanManager _manager;
    private readonly CapacityPlanService _service;
    private readonly PlanningGroupManager _groupManager;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapacityPlansController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The capacity plan manager.</param>
    /// <param name="service">The capacity plan service.</param>
    /// <param name="groupManager">The planning group manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public CapacityPlansController(
        IAuthorizationService authorizationService,
        CapacityPlanManager manager,
        CapacityPlanService service,
        PlanningGroupManager groupManager,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<CapacityPlansController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _service = service;
        _groupManager = groupManager;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the capacity plans.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/capacity-plans", "WorkforceCapacityPlans")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var groups = (await _groupManager.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(group => group.ItemId, group => group.Name, StringComparer.Ordinal);
        var items = (await _manager.GetAllAsync(HttpContext.RequestAborted)).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new ForecastingListViewModel
        {
            List = new AdminListContext<CapacityPlanRow>
            {
                Source = items
                    .Skip(pager.GetStartIndex())
                    .Take(pager.PageSize)
                    .Select(item => new CapacityPlanRow { Plan = item, PlanningGroupName = groups.GetValueOrDefault(item.PlanningGroupId ?? string.Empty) })
                    .ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows a plan week by week.
    /// </summary>
    /// <param name="id">The plan.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/capacity-plans/view/{id}", "WorkforceCapacityPlansView")]
    public async Task<IActionResult> Details(string id)
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

        return View(await _service.ProjectAsync(item, HttpContext.RequestAborted));
    }

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The plan, or <see langword="null"/> for a new one.</param>
    /// <param name="planningGroupId">The planning group of a new plan.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/capacity-plans/edit/{id?}", "WorkforceCapacityPlansEdit")]
    public async Task<IActionResult> Edit(string id, string planningGroupId)
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

        var isNew = string.IsNullOrEmpty(id);
        var model = new CapacityPlanEditViewModel
        {
            Id = id,
            Name = item.Name,
            PlanningGroupId = isNew ? planningGroupId : item.PlanningGroupId,
            StartDate = isNew ? Monday(DateOnly.FromDateTime(_clock.UtcNow).AddDays(7)) : item.StartDate,
            Weeks = item.Weeks,
            WeeklyWorkloadHours = item.WeeklyWorkloadHours,
            GrowthPercentPerYear = item.GrowthPercentPerYear,
            HoursPerFte = item.HoursPerFte,
            CurrentFte = isNew ? null : item.CurrentFte,
            AttritionPercentPerMonth = item.AttritionPercentPerMonth,
            HiringClasses = [.. item.HiringClasses.OrderBy(hiring => hiring.StartDate).Select(hiring => new HiringClassEditModel
            {
                StartDate = hiring.StartDate,
                Size = hiring.Size,
                TrainingWeeks = hiring.TrainingWeeks,
                RampWeeks = hiring.RampWeeks,
            })],
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves the editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The plan, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/capacity-plans/edit/{id?}", "WorkforceCapacityPlansEdit")]
    public async Task<IActionResult> EditPost(CapacityPlanEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var item = isNew ? await _manager.NewAsync() : await _manager.FindByIdAsync(form.Id);

        if (item is null)
        {
            return NotFound();
        }

        var group = string.IsNullOrEmpty(form.PlanningGroupId) ? null : await _groupManager.FindByIdAsync(form.PlanningGroupId);

        item.Name = form.Name?.Trim();
        item.PlanningGroupId = group?.ItemId;
        item.StartDate = Monday(form.StartDate);
        item.Weeks = form.Weeks;
        item.WeeklyWorkloadHours = form.WeeklyWorkloadHours;
        item.GrowthPercentPerYear = form.GrowthPercentPerYear;
        item.HoursPerFte = form.HoursPerFte;
        item.CurrentFte = form.CurrentFte ?? (group is null ? 0 : await _service.TeamHeadcountAsync(group, HttpContext.RequestAborted));
        item.AttritionPercentPerMonth = form.AttritionPercentPerMonth;
        item.HiringClasses = [.. (form.HiringClasses ?? [])
            .Where(hiring => hiring.StartDate.HasValue && hiring.Size.HasValue)
            .OrderBy(hiring => hiring.StartDate)
            .Select(hiring => new HiringClass
            {
                StartDate = hiring.StartDate.Value,
                Size = hiring.Size.Value,
                TrainingWeeks = hiring.TrainingWeeks,
                RampWeeks = hiring.RampWeeks,
            })];

        var validation = await _manager.ValidateAsync(item);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(form);

            return View(form);
        }

        if (isNew)
        {
            await _manager.CreateAsync(item);
        }
        else
        {
            await _manager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The capacity plan was saved."]);

        return RedirectToAction(nameof(Details), new { id = item.ItemId });
    }

    /// <summary>
    /// Deletes a capacity plan.
    /// </summary>
    /// <param name="id">The plan.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("workforce/capacity-plans/delete/{id}", "WorkforceCapacityPlansDelete")]
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
        await _notifier.SuccessAsync(H["The capacity plan was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private static DateOnly Monday(DateOnly date)
        => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private async Task PopulateAsync(CapacityPlanEditViewModel model)
    {
        while (model.HiringClasses.Count(hiring => !hiring.StartDate.HasValue && !hiring.Size.HasValue) < BlankHiringRows)
        {
            model.HiringClasses.Add(new HiringClassEditModel());
        }

        model.PlanningGroupOptions = (await _groupManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new SelectListItem(group.Name, group.ItemId, group.ItemId == model.PlanningGroupId))
            .ToList();
    }
}
