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
/// Planning groups: the queues forecast and staffed together, and their service goal.
/// </summary>
public sealed class PlanningGroupsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly PlanningGroupManager _manager;
    private readonly IActivityQueueManager _queueManager;
    private readonly ITeamManager _teamManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanningGroupsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The planning group manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public PlanningGroupsController(
        IAuthorizationService authorizationService,
        PlanningGroupManager manager,
        IActivityQueueManager queueManager,
        ITeamManager teamManager,
        INotifier notifier,
        IHtmlLocalizer<PlanningGroupsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _queueManager = queueManager;
        _teamManager = teamManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the planning groups.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/forecasting/planning-groups", "WorkforcePlanningGroups")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageForecasts))
        {
            return Forbid();
        }

        var items = (await _manager.GetAllAsync(HttpContext.RequestAborted)).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new ForecastingListViewModel
        {
            List = new AdminListContext<PlanningGroup>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The group, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/forecasting/planning-groups/edit/{id?}", "WorkforcePlanningGroupsEdit")]
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

        var model = new PlanningGroupEditViewModel
        {
            Id = id,
            Name = item.Name,
            QueueIds = [.. item.QueueIds],
            Channel = item.Channel?.ToString(),
            TeamId = item.TeamId,
            ServiceLevelPercent = item.ServiceLevelPercent,
            ServiceLevelSeconds = item.ServiceLevelSeconds,
            AverageSpeedOfAnswerSeconds = item.AverageSpeedOfAnswerSeconds,
            MaxOccupancyPercent = item.MaxOccupancyPercent,
            Concurrency = item.Concurrency,
            PatienceSeconds = item.PatienceSeconds,
            ShrinkagePercent = item.ShrinkagePercent,
            HistoryWeeks = item.HistoryWeeks,
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
    [Admin("workforce/forecasting/planning-groups/edit/{id?}", "WorkforcePlanningGroupsEdit")]
    public async Task<IActionResult> EditPost(PlanningGroupEditViewModel model)
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
        item.QueueIds = [.. (model.QueueIds ?? []).Where(queueId => !string.IsNullOrEmpty(queueId)).Distinct(StringComparer.Ordinal)];
        item.Channel = Enum.TryParse<InteractionChannel>(model.Channel, out var channel) ? channel : null;
        item.TeamId = string.IsNullOrEmpty(model.TeamId) ? null : model.TeamId;
        item.ServiceLevelPercent = model.ServiceLevelPercent;
        item.ServiceLevelSeconds = model.ServiceLevelSeconds;
        item.AverageSpeedOfAnswerSeconds = model.AverageSpeedOfAnswerSeconds;
        item.MaxOccupancyPercent = model.MaxOccupancyPercent;
        item.Concurrency = model.Concurrency;
        item.PatienceSeconds = model.PatienceSeconds;
        item.ShrinkagePercent = model.ShrinkagePercent;
        item.HistoryWeeks = model.HistoryWeeks;

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

        await _notifier.SuccessAsync(H["The planning group was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a planning group.
    /// </summary>
    /// <param name="id">The group.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("workforce/forecasting/planning-groups/delete/{id}", "WorkforcePlanningGroupsDelete")]
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
        await _notifier.SuccessAsync(H["The planning group was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(PlanningGroupEditViewModel model)
    {
        var chosen = model.QueueIds.ToHashSet(StringComparer.Ordinal);

        model.QueueOptions = (await _queueManager.GetAllAsync(HttpContext.RequestAborted))
            .Where(queue => !string.IsNullOrEmpty(queue.ItemId))
            .OrderBy(queue => queue.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(queue => new SelectListItem(queue.Name, queue.ItemId, chosen.Contains(queue.ItemId)))
            .ToList();
        model.TeamOptions = await WorkforceTeamOptions.ListAsync(_teamManager, WorkforceScope.Everything, [model.TeamId], HttpContext.RequestAborted);
    }
}
