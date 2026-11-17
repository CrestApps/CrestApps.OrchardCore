using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.Extensions.DependencyInjection;
using CrestApps.OrchardCore.Core.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Approvals.Controllers;

/// <summary>
/// Approvals › Policies: who may decide each kind of approval and how many must.
/// </summary>
[Admin]
public sealed class ApprovalPoliciesController : CatalogAdminController<ApprovalPolicy>
{
    public ApprovalPoliciesController(
        INamedCatalogManager<ApprovalPolicy> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<ApprovalPolicy> displayManager,
        INotifier notifier,
        IHtmlLocalizer<ApprovalPoliciesController> htmlLocalizer,
        IStringLocalizer<ApprovalPoliciesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    protected override Permission ManagePermission
        => ApprovalsPermissions.ManageApprovalPolicies;

    protected override LocalizedString CreateDisplayName
        => S["Approval policy"];

    protected override LocalizedString NewDisplayName
        => S["New approval policy"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The approval policy has been added."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The approval policy has been updated. Requests already made keep the number of approvals they needed."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The approval policy has been removed."];

    /// <inheritdoc/>
    /// <remarks>People know a policy by its title, such as "Pay run approval"; the technical name is the fallback.</remarks>
    protected override string GetName(ApprovalPolicy entry)
        => string.IsNullOrWhiteSpace(entry?.Title) ? base.GetName(entry) : entry.Title;

    /// <summary>
    /// The view data key of the approval types "Add approval policy" offers.
    /// </summary>
    public const string TypesViewDataKey = "ApprovalTypes";

    /// <inheritdoc/>
    /// <remarks>
    /// A policy serves one approval type, chosen in "Add approval policy" (<c>?type=PayRun</c>) and fixed afterwards:
    /// the type decides the conditions and fields its editor shows. With one type on offer, it is that one.
    /// </remarks>
    protected override Task<bool> InitializeNewAsync(ApprovalPolicy model)
    {
        var types = HttpContext.RequestServices.GetRequiredService<IApprovalTypeCatalog>().List();
        var requested = Request.Query["type"].ToString();

        if (string.IsNullOrWhiteSpace(requested) && types.Count == 1)
        {
            requested = types[0].Name;
        }

        if (types.FirstOrDefault(type => string.Equals(type.Name, requested, StringComparison.OrdinalIgnoreCase)) is not { } chosen)
        {
            return Task.FromResult(false);
        }

        model.Type = chosen.Name;

        return Task.FromResult(true);
    }

    [Admin("approvals/policies", "ApprovalPoliciesIndex")]
    public Task<IActionResult> Index()
    {
        ViewData[TypesViewDataKey] = HttpContext.RequestServices.GetService<IApprovalTypeCatalog>()?.List() ?? [];

        return IndexAllAsync();
    }

    [Admin("approvals/policies/create", "ApprovalPoliciesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("approvals/policies/create", "ApprovalPoliciesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("approvals/policies/edit/{id}", "ApprovalPoliciesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("approvals/policies/edit/{id}", "ApprovalPoliciesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("approvals/policies/delete/{id}", "ApprovalPoliciesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
