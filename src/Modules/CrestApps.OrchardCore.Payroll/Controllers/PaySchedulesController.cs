using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// Payroll › Setup › Pay Schedules.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.PaySetup)]
public sealed class PaySchedulesController : CatalogAdminController<PaySchedule>
{
    private readonly IEnumerable<IPaySetupUsage> _usages;

    public PaySchedulesController(
        IPayScheduleManager manager,
        IEnumerable<IPaySetupUsage> usages,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<PaySchedule> displayManager,
        INotifier notifier,
        IHtmlLocalizer<PaySchedulesController> htmlLocalizer,
        IStringLocalizer<PaySchedulesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _usages = usages;
    }

    protected override Permission ManagePermission
        => PayrollPermissions.ManagePaySetup;

    protected override LocalizedString CreateDisplayName
        => S["Pay schedule"];

    protected override LocalizedString NewDisplayName
        => S["New pay schedule"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The pay schedule has been added."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The pay schedule has been updated. Timesheets and pay runs already made keep their periods."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The pay schedule has been removed."];

    // A record other modules point to is kept while used: the list says what uses it.
    protected override async Task<bool> CanDeleteAsync(PaySchedule model)
    {
        foreach (var usage in _usages)
        {
            if (await usage.GetUsagesAsync(model, HttpContext.RequestAborted) is { Count: > 0 } uses)
            {
                await Notifier.ErrorAsync(H["It is in use and can't be removed: {0}. Turn it off instead.", string.Join(", ", uses)]);

                return false;
            }
        }

        return true;
    }

    [Admin("payroll/setup/pay-schedules", "PaySchedulesIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    [Admin("payroll/setup/pay-schedules/create", "PaySchedulesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("payroll/setup/pay-schedules/create", "PaySchedulesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("payroll/setup/pay-schedules/edit/{id}", "PaySchedulesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("payroll/setup/pay-schedules/edit/{id}", "PaySchedulesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("payroll/setup/pay-schedules/delete/{id}", "PaySchedulesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
