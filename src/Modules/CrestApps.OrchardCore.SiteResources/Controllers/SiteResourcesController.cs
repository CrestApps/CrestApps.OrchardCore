using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.SiteResources.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.SiteResources.Controllers;

/// <summary>
/// Design › Site Resources: the stylesheets and scripts the site adds to its pages.
/// </summary>
[Admin]
[Feature(SiteResourcesConstants.Features.SiteResources)]
public sealed class SiteResourcesController : CatalogAdminController<SiteResource>
{
    public SiteResourcesController(
        ICatalogManager<SiteResource> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<SiteResource> displayManager,
        INotifier notifier,
        IHtmlLocalizer<SiteResourcesController> htmlLocalizer,
        IStringLocalizer<SiteResourcesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    protected override Permission ManagePermission
        => SiteResourcesPermissions.ManageSiteResources;

    protected override LocalizedString CreateDisplayName
        => S["Site resource"];

    protected override LocalizedString NewDisplayName
        => S["New site resource"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The site resource has been added."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The site resource has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The site resource has been removed. Pages no longer load it."];

    // Listed in the order the resources added to every page are added.
    protected override IEnumerable<SiteResource> OrderAll(IEnumerable<SiteResource> entries)
        => entries
            .OrderBy(entry => entry.Position)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

    [Admin("site-resources", "SiteResourcesIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("site-resources", "SiteResourcesIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    [Admin("site-resources/create", "SiteResourcesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("site-resources/create", "SiteResourcesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("site-resources/edit/{id}", "SiteResourcesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("site-resources/edit/{id}", "SiteResourcesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("site-resources/delete/{id}", "SiteResourcesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
