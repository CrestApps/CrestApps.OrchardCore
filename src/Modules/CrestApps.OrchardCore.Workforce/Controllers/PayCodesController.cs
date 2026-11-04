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
/// The pay codes: the list and the editor.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class PayCodesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IPayCodeManager _manager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayCodesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayCodesController(
        IAuthorizationService authorizationService,
        IPayCodeManager manager,
        INotifier notifier,
        IHtmlLocalizer<PayCodesController> htmlLocalizer,
        IStringLocalizer<PayCodesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _notifier = notifier;
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
    [Admin("workforce/pay-codes", "WorkforcePayCodes")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var items = (await _manager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(item => item.Code, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<PayCode>
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
    [Admin("workforce/pay-codes/edit/{id?}", "WorkforcePayCodesEdit")]
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

        var model = new PayCodeEditViewModel
        {
            Id = id,
            Code = item.Code,
            Name = item.Name,
            Kind = item.Kind.ToString(),
            Multiplier = item.Multiplier,
            PremiumPerHour = item.PremiumPerHour,
            WindowStart = item.WindowStart,
            WindowEnd = item.WindowEnd,
            Weekdays = item.Weekdays.Select(day => day.ToString()).ToList(),
            ExportCode = item.ExportCode,
            Enabled = item.Enabled,
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
    [Admin("workforce/pay-codes/edit/{id?}", "WorkforcePayCodesEdit")]
    public async Task<IActionResult> EditPost(PayCodeEditViewModel model)
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

        item.Code = model.Code;
        item.Name = model.Name;
        item.Kind = Enum.TryParse<PayCodeKind>(model.Kind, out var kind) ? kind : PayCodeKind.Regular;
        item.Multiplier = model.Multiplier;
        item.PremiumPerHour = item.Kind == PayCodeKind.ShiftDifferential ? model.PremiumPerHour : null;
        item.WindowStart = item.Kind == PayCodeKind.ShiftDifferential ? model.WindowStart : null;
        item.WindowEnd = item.Kind == PayCodeKind.ShiftDifferential ? model.WindowEnd : null;
        item.Weekdays = item.Kind == PayCodeKind.ShiftDifferential
            ? (model.Weekdays ?? []).Select(day => Enum.TryParse<DayOfWeek>(day, out var parsed) ? parsed : (DayOfWeek?)null).Where(day => day.HasValue).Select(day => day.Value).Distinct().ToList()
            : [];
        item.ExportCode = model.ExportCode;
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

        await _notifier.SuccessAsync(H["The pay code has been saved."]);

        return AfterSave(item, isNew);
    }

    /// <summary>
    /// Deletes it.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/pay-codes/delete/{id}", "WorkforcePayCodesDelete")]
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
            await _notifier.SuccessAsync(H["The pay code has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private RedirectToActionResult AfterSave(PayCode item, bool isNew)
    {
        return RedirectToAction(nameof(Index));
    }

    private static Task PopulateAsync(PayCodeEditViewModel model)
    {
        return Task.CompletedTask;
    }
}
