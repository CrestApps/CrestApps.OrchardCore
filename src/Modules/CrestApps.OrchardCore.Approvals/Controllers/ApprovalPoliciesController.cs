using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
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

    [Admin("approvals/policies", "ApprovalPoliciesIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

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
