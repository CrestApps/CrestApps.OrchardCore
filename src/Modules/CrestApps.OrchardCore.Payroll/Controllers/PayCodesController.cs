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
/// Payroll › Setup › Pay Codes.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.PaySetup)]
public sealed class PayCodesController : CatalogAdminController<PayCode>
{
    private readonly IEnumerable<IPaySetupUsage> _usages;

    public PayCodesController(
        IPayCodeManager manager,
        IEnumerable<IPaySetupUsage> usages,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<PayCode> displayManager,
        INotifier notifier,
        IHtmlLocalizer<PayCodesController> htmlLocalizer,
        IStringLocalizer<PayCodesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _usages = usages;
    }

    protected override Permission ManagePermission
        => PayrollPermissions.ManagePaySetup;

    protected override LocalizedString CreateDisplayName
        => S["Pay code"];

    protected override LocalizedString NewDisplayName
        => S["New pay code"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The pay code has been added."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The pay code has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The pay code has been removed."];

    // A record other modules point to is kept while used: the list says what uses it.
    protected override async Task<bool> CanDeleteAsync(PayCode model)
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

    [Admin("payroll/setup/pay-codes", "PayCodesIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    [Admin("payroll/setup/pay-codes/create", "PayCodesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("payroll/setup/pay-codes/create", "PayCodesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("payroll/setup/pay-codes/edit/{id}", "PayCodesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("payroll/setup/pay-codes/edit/{id}", "PayCodesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("payroll/setup/pay-codes/delete/{id}", "PayCodesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
