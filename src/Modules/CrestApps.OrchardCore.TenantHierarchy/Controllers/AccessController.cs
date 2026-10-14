using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.Controllers;

/// <summary>
/// Manages who may enter the child tenants of a parent, and with which child roles.
/// </summary>
[Admin]
[Feature(TenantHierarchyConstants.Features.Parent)]
public sealed class AccessController : Controller
{
    private readonly AccessGrantManager _grantManager;
    private readonly ITenantHierarchyBroker _broker;
    private readonly HierarchyLabelsProvider _labelsProvider;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ShellSettings _shellSettings;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessController"/> class.
    /// </summary>
    /// <param name="grantManager">The access grant manager.</param>
    /// <param name="broker">The tenant hierarchy broker.</param>
    /// <param name="labelsProvider">The labels provider.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="shellSettings">The settings of the parent tenant.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AccessController(
        AccessGrantManager grantManager,
        ITenantHierarchyBroker broker,
        HierarchyLabelsProvider labelsProvider,
        IAuthorizationService authorizationService,
        INotifier notifier,
        ShellSettings shellSettings,
        IHtmlLocalizer<AccessController> htmlLocalizer)
    {
        _grantManager = grantManager;
        _broker = broker;
        _labelsProvider = labelsProvider;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _shellSettings = shellSettings;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows the grants for every child tenant.
    /// </summary>
    [Admin("children/access", "TenantHierarchyAccess")]
    public async Task<IActionResult> Index()
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return View(await BuildModelAsync(null, new AccessGrantsViewModel()));
    }

    /// <summary>
    /// Shows the grants for one child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [Admin("children/{id}/access", "TenantHierarchyChildAccess")]
    public async Task<IActionResult> Child(string id)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var child = await _broker.GetChildAsync(id);

        if (child is null || child.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            return NotFound();
        }

        return View(nameof(Index), await BuildModelAsync(child, new AccessGrantsViewModel()));
    }

    /// <summary>
    /// Adds a grant, for one child tenant or for every child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier, or <see langword="null"/> for every child tenant.</param>
    /// <param name="model">The new grant.</param>
    [HttpPost]
    [Admin("children/access/add", "TenantHierarchyAccessAdd")]
    public async Task<IActionResult> Add(string id, AccessGrantsViewModel model)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var principal = model.PrincipalType == AccessGrantPrincipalType.Role
            ? model.PrincipalRole
            : model.Principal;

        var result = await _grantManager.AddAsync(model.PrincipalType, principal, string.IsNullOrEmpty(id) ? null : id, model.SelectedChildRoles);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Access was granted."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Error]);
        }

        return RedirectBack(id);
    }

    /// <summary>
    /// Removes a grant.
    /// </summary>
    /// <param name="grantId">The grant identifier.</param>
    /// <param name="id">The registry entry to return to, or <see langword="null"/>.</param>
    [HttpPost]
    [Admin("children/access/{grantId}/remove", "TenantHierarchyAccessRemove")]
    public async Task<IActionResult> Remove(string grantId, string id)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var result = await _grantManager.RemoveAsync(grantId);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Access was removed. Open sessions end at their next check."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Error]);
        }

        return RedirectBack(id);
    }

    private RedirectToActionResult RedirectBack(string id)
    {
        return string.IsNullOrEmpty(id)
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Child), new { id });
    }

    private async Task<AccessGrantsViewModel> BuildModelAsync(ChildTenantInfo child, AccessGrantsViewModel model)
    {
        model.Child = child;
        model.Labels = _labelsProvider.GetLabels();
        model.ParentName = _shellSettings.GetHierarchyDisplayName() ?? model.Labels.Parent;
        model.Grants = (await _grantManager.ListAsync(child?.Entry.EntryId)).ToList();
        model.InheritedGrants = child is null
            ? []
            : (await _grantManager.ListAsync(null)).ToList();
        model.ParentRoles = (await _grantManager.GetParentRolesAsync()).ToList();
        model.ChildRoles = (await _broker.GetChildRolesAsync(child?.Entry.EntryId)).ToList();

        return model;
    }

    private async Task<bool> CanManageAsync()
    {
        return _shellSettings.IsParentTenant() &&
            await _authorizationService.AuthorizeAsync(User, TenantHierarchyPermissions.ManageChildAccess);
    }
}
