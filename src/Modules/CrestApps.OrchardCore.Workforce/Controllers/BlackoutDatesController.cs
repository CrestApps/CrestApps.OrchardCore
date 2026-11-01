using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The blackout dates: the list and the editor.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
public sealed class BlackoutDatesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IBlackoutPeriodManager _blackoutManager;
    private readonly ITimeOffTypeManager _typeManager;
    private readonly ITeamManager _teamManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlackoutDatesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="blackoutManager">The manager.</param>
    /// <param name="typeManager">The time-off type manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BlackoutDatesController(
        IAuthorizationService authorizationService,
        IBlackoutPeriodManager blackoutManager,
        ITimeOffTypeManager typeManager,
        ITeamManager teamManager,
        INotifier notifier,
        IHtmlLocalizer<BlackoutDatesController> htmlLocalizer,
        IStringLocalizer<BlackoutDatesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _blackoutManager = blackoutManager;
        _typeManager = typeManager;
        _teamManager = teamManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the blackout dates.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/blackout-dates", "WorkforceBlackoutDates")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var items = (await _blackoutManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderByDescending(item => item.StartDate)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<BlackoutPeriod>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of new or existing blackout dates.
    /// </summary>
    /// <param name="id">The id, or empty for new.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/blackout-dates/edit/{id?}", "WorkforceBlackoutDatesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _blackoutManager.NewAsync() : await _blackoutManager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = new BlackoutPeriodEditViewModel
        {
            Id = id,
            Name = item.Name,
            TeamId = item.TeamId,
            StartDate = string.IsNullOrEmpty(id) ? null : item.StartDate,
            EndDate = string.IsNullOrEmpty(id) ? null : item.EndDate,
            TypeIds = item.TypeIds.ToArray(),
            Message = item.Message,
            Enabled = item.Enabled,
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves new or existing blackout dates.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/blackout-dates/edit/{id?}", "WorkforceBlackoutDatesEdit")]
    public async Task<IActionResult> EditPost(BlackoutPeriodEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var item = isNew ? await _blackoutManager.NewAsync() : await _blackoutManager.FindByIdAsync(model.Id);

        if (item is null)
        {
            return NotFound();
        }

        if (!model.StartDate.HasValue || !model.EndDate.HasValue)
        {
            ModelState.AddModelError(nameof(model.StartDate), S["Enter the first and the last day."]);
        }

        item.Name = model.Name;
        item.TeamId = string.IsNullOrEmpty(model.TeamId) ? null : model.TeamId;
        item.StartDate = model.StartDate ?? default;
        item.EndDate = model.EndDate ?? model.StartDate ?? default;
        item.TypeIds = (model.TypeIds ?? []).Where(typeId => !string.IsNullOrEmpty(typeId)).Distinct(StringComparer.Ordinal).ToList();
        item.Message = model.Message?.Trim();
        item.Enabled = model.Enabled;

        if (ModelState.IsValid)
        {
            var validation = await _blackoutManager.ValidateAsync(item);

            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);

            return View(model);
        }

        if (isNew)
        {
            await _blackoutManager.CreateAsync(item);
        }
        else
        {
            await _blackoutManager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The blackout dates have been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes blackout dates.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/blackout-dates/delete/{id}", "WorkforceBlackoutDatesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var item = await _blackoutManager.FindByIdAsync(id);

        if (item is not null)
        {
            await _blackoutManager.DeleteAsync(item);
            await _notifier.SuccessAsync(H["The blackout dates have been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(BlackoutPeriodEditViewModel model)
    {
        var selectedTypes = new HashSet<string>(model.TypeIds ?? [], StringComparer.Ordinal);
        var selectedTeams = new HashSet<string>([model.TeamId ?? string.Empty], StringComparer.Ordinal);

        model.TypeOptions = (await _typeManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(type => new SelectListItem(type.Name, type.ItemId, selectedTypes.Contains(type.ItemId)))
            .ToList();
        model.TeamOptions = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, selectedTeams.Contains(item.Team.ItemId)))
            .ToList();
    }
}
