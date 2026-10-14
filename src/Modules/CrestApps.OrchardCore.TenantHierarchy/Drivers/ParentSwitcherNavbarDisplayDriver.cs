using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.Admin.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Adds a shortcut to the tenant picker to the admin navbar of a parent tenant, for users who may enter child tenants.
/// </summary>
public sealed class ParentSwitcherNavbarDisplayDriver : DisplayDriver<Navbar>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly HierarchyLabelsProvider _labelsProvider;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentSwitcherNavbarDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="labelsProvider">The labels provider.</param>
    /// <param name="shellSettings">The settings of the parent tenant.</param>
    public ParentSwitcherNavbarDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        HierarchyLabelsProvider labelsProvider,
        ShellSettings shellSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _labelsProvider = labelsProvider;
        _shellSettings = shellSettings;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(Navbar model, BuildDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!_shellSettings.IsParentTenant() ||
            user?.Identity?.IsAuthenticated != true ||
            !await _authorizationService.AuthorizeAsync(user, TenantHierarchyPermissions.EnterChildTenants))
        {
            return null;
        }

        return Initialize<ParentSwitcherNavbarViewModel>("ParentSwitcherNavbarItem", viewModel =>
        {
            var labels = _labelsProvider.GetLabels();
            viewModel.ChildLabel = labels.Child;
            viewModel.ChildrenLabel = labels.Children;
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:2");
    }
}
