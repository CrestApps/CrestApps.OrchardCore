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
/// The time clock kiosks: the list, the editor and their private addresses.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class TimeClockKiosksController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITimeClockKioskManager _manager;
    private readonly INotifier _notifier;
    private readonly ITeamManager _teamManager;
    private readonly ITimeClockService _timeClock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockKiosksController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="timeClock">The time clock.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeClockKiosksController(
        IAuthorizationService authorizationService,
        ITimeClockKioskManager manager,
        INotifier notifier,
        ITeamManager teamManager,
        ITimeClockService timeClock,
        IHtmlLocalizer<TimeClockKiosksController> htmlLocalizer,
        IStringLocalizer<TimeClockKiosksController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _notifier = notifier;
        _teamManager = teamManager;
        _timeClock = timeClock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists them.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/time-clock-kiosks", "WorkforceTimeClockKiosks")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var items = (await _manager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<TimeClockKiosk>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The id, or empty for new.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/time-clock-kiosks/edit/{id?}", "WorkforceTimeClockKiosksEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _manager.NewAsync() : await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = new TimeClockKioskEditViewModel
        {
            Id = id,
            Name = item.Name,
            TeamId = item.TeamId,
            Enabled = item.Enabled,
            NewAddress = TempData["KioskToken"] is string token ? KioskUrl(token) : null,
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves it.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/time-clock-kiosks/edit/{id?}", "WorkforceTimeClockKiosksEdit")]
    public async Task<IActionResult> EditPost(TimeClockKioskEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var item = isNew ? await _manager.NewAsync() : await _manager.FindByIdAsync(model.Id);

        if (item is null)
        {
            return NotFound();
        }

        item.Name = model.Name;
        item.TeamId = string.IsNullOrEmpty(model.TeamId) ? null : model.TeamId;
        item.Enabled = model.Enabled;

        if (ModelState.IsValid)
        {
            var validation = await _manager.ValidateAsync(item);

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
            await _manager.CreateAsync(item);
        }
        else
        {
            await _manager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The kiosk has been saved."]);

        return AfterSave(item, isNew);
    }

    /// <summary>
    /// Deletes it.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/time-clock-kiosks/delete/{id}", "WorkforceTimeClockKiosksDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var item = await _manager.FindByIdAsync(id);

        if (item is not null)
        {
            await _manager.DeleteAsync(item);
            await _notifier.SuccessAsync(H["The kiosk has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Gives a kiosk a new address, shown once, which stops the old one working.
    /// </summary>
    /// <param name="id">The kiosk.</param>
    /// <param name="created">Whether the kiosk was just created.</param>
    /// <returns>A redirect to the editor, which shows the address.</returns>
    [HttpGet]
    [Admin("workforce/time-clock-kiosks/address/{id}", "WorkforceTimeClockKiosksAddress")]
    public async Task<IActionResult> Renew(string id, bool created = false)
    {
        if (!created)
        {
            return RedirectToAction(nameof(Edit), new { id });
        }

        return await RenewPost(id);
    }

    /// <summary>
    /// Gives a kiosk a new address, shown once, which stops the old one working.
    /// </summary>
    /// <param name="id">The kiosk.</param>
    /// <returns>A redirect to the editor, which shows the address.</returns>
    [HttpPost]
    [ActionName(nameof(Renew))]
    [Admin("workforce/time-clock-kiosks/address/{id}", "WorkforceTimeClockKiosksAddress")]
    public async Task<IActionResult> RenewPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var item = await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        TempData["KioskToken"] = await _timeClock.RenewKioskTokenAsync(item, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Edit), new { id });
    }

    private RedirectToActionResult AfterSave(TimeClockKiosk item, bool isNew)
    {
        return isNew ? RedirectToAction(nameof(Renew), new { id = item.ItemId, created = true }) : RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(TimeClockKioskEditViewModel model)
    {
        model.TeamOptions = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(entry => new SelectListItem(new string('\u00A0', entry.Depth * 3) + entry.Team.Name, entry.Team.ItemId, entry.Team.ItemId == model.TeamId))
            .ToList();
    }

    private string KioskUrl(string token)
    {
        return Url.Content("~/workforce/kiosk/" + token);
    }
}
