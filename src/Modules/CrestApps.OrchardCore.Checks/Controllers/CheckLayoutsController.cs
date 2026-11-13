using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Checks.Controllers;

/// <summary>
/// Banking › Check Layouts: how checks print on the business's stock.
/// </summary>
[Admin]
public sealed class CheckLayoutsController : CatalogAdminController<CheckLayout>
{
    public CheckLayoutsController(
        INamedCatalogManager<CheckLayout> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<CheckLayout> displayManager,
        INotifier notifier,
        IHtmlLocalizer<CheckLayoutsController> htmlLocalizer,
        IStringLocalizer<CheckLayoutsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    protected override Permission ManagePermission
        => ChecksPermissions.ManageCheckLayouts;

    protected override LocalizedString CreateDisplayName
        => S["Check layout"];

    protected override LocalizedString NewDisplayName
        => S["New check layout"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The check layout has been added. Print a check on plain paper and hold it against the stock to calibrate it."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The check layout has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The check layout has been removed."];

    [Admin("banking/check-layouts", "CheckLayoutsIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    [Admin("banking/check-layouts/create", "CheckLayoutsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("banking/check-layouts/create", "CheckLayoutsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("banking/check-layouts/edit/{id}", "CheckLayoutsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("banking/check-layouts/edit/{id}", "CheckLayoutsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("banking/check-layouts/delete/{id}", "CheckLayoutsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
