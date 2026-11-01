using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The time-off types: the list and the editor.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
public sealed class TimeOffTypesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITimeOffTypeManager _typeManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffTypesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="typeManager">The manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffTypesController(
        IAuthorizationService authorizationService,
        ITimeOffTypeManager typeManager,
        INotifier notifier,
        IHtmlLocalizer<TimeOffTypesController> htmlLocalizer,
        IStringLocalizer<TimeOffTypesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _typeManager = typeManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the time-off types.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/time-off-types", "WorkforceTimeOffTypes")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var items = (await _typeManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<TimeOffType>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of new or existing time-off types.
    /// </summary>
    /// <param name="id">The id, or empty for new.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/time-off-types/edit/{id?}", "WorkforceTimeOffTypesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _typeManager.NewAsync() : await _typeManager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = new TimeOffTypeEditViewModel
        {
            Id = id,
            Name = item.Name,
            Description = item.Description,
            Color = item.Color ?? "#e8590c",
            Unit = item.Unit.ToString(),
            HoursPerDay = item.HoursPerDay,
            IsPaid = item.IsPaid,
            RequiresBalance = item.RequiresBalance,
            AllowPartialDay = item.AllowPartialDay,
            RequiresReason = item.RequiresReason,
            CountsAgainstAllotment = item.CountsAgainstAllotment,
            PayCode = item.PayCode,
            VisibleToWorkers = item.VisibleToWorkers,
            Enabled = item.Enabled,
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves new or existing time-off types.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/time-off-types/edit/{id?}", "WorkforceTimeOffTypesEdit")]
    public async Task<IActionResult> EditPost(TimeOffTypeEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var item = isNew ? await _typeManager.NewAsync() : await _typeManager.FindByIdAsync(model.Id);

        if (item is null)
        {
            return NotFound();
        }

        item.Name = model.Name;
        item.Description = model.Description?.Trim();
        item.Color = string.IsNullOrWhiteSpace(model.Color) ? null : model.Color.Trim();
        item.Unit = Enum.TryParse<TimeOffUnit>(model.Unit, out var unit) ? unit : TimeOffUnit.Hours;
        item.HoursPerDay = model.HoursPerDay;
        item.IsPaid = model.IsPaid;
        item.RequiresBalance = model.RequiresBalance;
        item.AllowPartialDay = model.AllowPartialDay;
        item.RequiresReason = model.RequiresReason;
        item.CountsAgainstAllotment = model.CountsAgainstAllotment;
        item.PayCode = string.IsNullOrWhiteSpace(model.PayCode) ? null : model.PayCode.Trim();
        item.VisibleToWorkers = model.VisibleToWorkers;
        item.Enabled = model.Enabled;

        if (ModelState.IsValid)
        {
            var validation = await _typeManager.ValidateAsync(item);

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
            await _typeManager.CreateAsync(item);
        }
        else
        {
            await _typeManager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The time-off type has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes time-off types.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/time-off-types/delete/{id}", "WorkforceTimeOffTypesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var item = await _typeManager.FindByIdAsync(id);

        if (item is not null)
        {
            await _typeManager.DeleteAsync(item);
            await _notifier.SuccessAsync(H["The time-off type has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private static Task PopulateAsync(TimeOffTypeEditViewModel model)
    {
        return Task.CompletedTask;
    }
}
