using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
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

namespace CrestApps.OrchardCore.BusinessDocuments.Controllers;

/// <summary>
/// Settings › Business Documents › Document Templates: the looks business documents are printed with.
/// </summary>
[Admin]
[Feature(BusinessDocumentsConstants.Features.Area)]
public sealed class BusinessDocumentTemplatesController : CatalogAdminController<BusinessDocumentTemplate>
{
    public BusinessDocumentTemplatesController(
        IBusinessDocumentTemplateManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<BusinessDocumentTemplate> displayManager,
        INotifier notifier,
        IHtmlLocalizer<BusinessDocumentTemplatesController> htmlLocalizer,
        IStringLocalizer<BusinessDocumentTemplatesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    protected override Permission ManagePermission
        => BusinessDocumentsPermissions.ManageBusinessDocumentTemplates;

    protected override LocalizedString CreateDisplayName
        => S["Document template"];

    protected override LocalizedString NewDisplayName
        => S["New document template"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The document template has been added."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The document template has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The document template has been removed. Documents of its kinds are printed with another default template, or with none."];

    [Admin("business-documents/templates", "BusinessDocumentTemplatesIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("business-documents/templates", "BusinessDocumentTemplatesIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    [Admin("business-documents/templates/create", "BusinessDocumentTemplatesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("business-documents/templates/create", "BusinessDocumentTemplatesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("business-documents/templates/edit/{id}", "BusinessDocumentTemplatesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("business-documents/templates/edit/{id}", "BusinessDocumentTemplatesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("business-documents/templates/delete/{id}", "BusinessDocumentTemplatesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);

    // Only one template is the default for a kind: a template saved as the default takes over from the others that
    // claim the same kinds, or that claim every kind when it does too.
    protected override async Task OnSavedAsync(BusinessDocumentTemplate model, bool isNew)
    {
        if (!model.IsDefault)
        {
            return;
        }

        foreach (var other in await Manager.GetAllAsync())
        {
            if (other.ItemId != model.ItemId && other.IsDefault && ClaimTheSameKinds(model, other))
            {
                other.IsDefault = false;
                await Manager.UpdateAsync(other);
            }
        }
    }

    private static bool ClaimTheSameKinds(BusinessDocumentTemplate first, BusinessDocumentTemplate second)
    {
        var firstKinds = first.Kinds ?? [];
        var secondKinds = second.Kinds ?? [];

        if (firstKinds.Count == 0 || secondKinds.Count == 0)
        {
            return firstKinds.Count == 0 && secondKinds.Count == 0;
        }

        return firstKinds.Intersect(secondKinds, StringComparer.OrdinalIgnoreCase).Any();
    }
}
