using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Taxation.Controllers;

/// <summary>
/// Commerce &gt; Taxation &gt; Tax registrations: where the business is registered to collect a tax, under which number,
/// and, with Tax Returns, how often it files and when its returns are due.
/// </summary>
[Admin]
public sealed class TaxRegistrationsController : CatalogAdminController<MerchantTaxRegistration>
{
    public TaxRegistrationsController(
        ICatalogManager<MerchantTaxRegistration> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<MerchantTaxRegistration> displayManager,
        INotifier notifier,
        IHtmlLocalizer<TaxRegistrationsController> htmlLocalizer,
        IStringLocalizer<TaxRegistrationsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    protected override Permission ManagePermission
        => TaxationPermissions.ManageTaxation;

    protected override LocalizedString CreateDisplayName
        => S["Tax registration"];

    protected override LocalizedString NewDisplayName
        => S["New tax registration"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The tax registration has been added."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The tax registration has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The tax registration has been removed."];

    /// <inheritdoc/>
    /// <remarks>
    /// A registration with returns is kept: its returns are records of what the business filed. Make it inactive
    /// instead.
    /// </remarks>
    protected override async Task<bool> CanDeleteAsync(MerchantTaxRegistration model)
    {
        if (HttpContext.RequestServices.GetService<ITaxReturnManager>() is not { } returns || !await returns.HasReturnsAsync(model.ItemId))
        {
            return true;
        }

        await Notifier.ErrorAsync(H["The registration {0} has tax returns, so it is kept: make it inactive instead.", model.Name]);

        return false;
    }

    [Admin("taxation/registrations", "TaxationRegistrationsIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    [Admin("taxation/registrations/create", "TaxationRegistrationsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("taxation/registrations/create", "TaxationRegistrationsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("taxation/registrations/edit/{id}", "TaxationRegistrationsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("taxation/registrations/edit/{id}", "TaxationRegistrationsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("taxation/registrations/delete/{id}", "TaxationRegistrationsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
