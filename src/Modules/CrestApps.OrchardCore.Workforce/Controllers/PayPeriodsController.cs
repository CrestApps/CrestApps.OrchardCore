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
/// The pay period calendars: the list and the editor.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class PayPeriodsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IPayPeriodCalendarManager _manager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayPeriodsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayPeriodsController(
        IAuthorizationService authorizationService,
        IPayPeriodCalendarManager manager,
        INotifier notifier,
        IHtmlLocalizer<PayPeriodsController> htmlLocalizer,
        IStringLocalizer<PayPeriodsController> stringLocalizer)
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
    [Admin("workforce/pay-periods", "WorkforcePayPeriods")]
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
            List = new AdminListContext<PayPeriodCalendar>
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
    [Admin("workforce/pay-periods/edit/{id?}", "WorkforcePayPeriodsEdit")]
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

        var model = new PayPeriodEditViewModel
        {
            Id = id,
            Name = item.Name,
            Frequency = item.Frequency.ToString(),
            AnchorDate = item.AnchorDate,
            WeekStart = item.WeekStart.ToString(),
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
    [Admin("workforce/pay-periods/edit/{id?}", "WorkforcePayPeriodsEdit")]
    public async Task<IActionResult> EditPost(PayPeriodEditViewModel model)
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
        item.Frequency = Enum.TryParse<PayPeriodFrequency>(model.Frequency, out var frequency) ? frequency : PayPeriodFrequency.Biweekly;
        item.AnchorDate = model.AnchorDate == default ? item.AnchorDate : model.AnchorDate;
        item.WeekStart = Enum.TryParse<DayOfWeek>(model.WeekStart, out var weekStart) ? weekStart : DayOfWeek.Sunday;
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

        await _notifier.SuccessAsync(H["The pay periods have been saved."]);

        return AfterSave(item, isNew);
    }

    /// <summary>
    /// Deletes it.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/pay-periods/delete/{id}", "WorkforcePayPeriodsDelete")]
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
            await _notifier.SuccessAsync(H["The pay periods have been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private RedirectToActionResult AfterSave(PayPeriodCalendar item, bool isNew)
    {
        return RedirectToAction(nameof(Index));
    }

    private static Task PopulateAsync(PayPeriodEditViewModel model)
    {
        var preview = new PayPeriodCalendar
        {
            Frequency = Enum.TryParse<PayPeriodFrequency>(model.Frequency, out var frequency) ? frequency : PayPeriodFrequency.Biweekly,
            AnchorDate = model.AnchorDate == default ? new PayPeriodCalendar().AnchorDate : model.AnchorDate,
        };
        var date = DateOnly.FromDateTime(DateTime.Today);

        for (var index = 0; index < 4; index++)
        {
            var period = preview.PeriodOn(date);

            model.Upcoming.Add(period);
            date = period.End.AddDays(1);
        }

        return Task.CompletedTask;
    }
}
