using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.Admin.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Adds the tenant switcher to the admin navbar of a child tenant, for users that entered through delegated access.
/// It shows the parent's name and links to the parent; the list of other child tenants is served by the parent, so
/// it never enters the child's page.
/// </summary>
public sealed class TenantSwitcherNavbarDisplayDriver : DisplayDriver<Navbar>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantSwitcherNavbarDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="shellSettings">The settings of the child tenant.</param>
    public TenantSwitcherNavbarDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        ShellSettings shellSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _shellSettings = shellSettings;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(Navbar model, BuildDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!_shellSettings.IsChildTenant() || !DelegatedAccessClaims.IsDelegated(user))
        {
            return null;
        }

        var parentAddress = user.FindFirst(TenantHierarchyConstants.ClaimTypes.ParentAddress)?.Value;

        if (string.IsNullOrEmpty(parentAddress) || !Uri.TryCreate(parentAddress, UriKind.Absolute, out _))
        {
            return null;
        }

        var embedded = string.Equals(
            user.FindFirst(TenantHierarchyConstants.ClaimTypes.SwitcherMode)?.Value,
            nameof(SwitcherMode.Embedded),
            StringComparison.OrdinalIgnoreCase);

        return Initialize<TenantSwitcherNavbarViewModel>("TenantSwitcherNavbarItem", viewModel =>
        {
            viewModel.ParentName = user.FindFirst(TenantHierarchyConstants.ClaimTypes.ParentDisplayName)?.Value;
            viewModel.ChildName = _shellSettings[TenantHierarchyConstants.SettingsKeys.Description] ?? _shellSettings.Name;
            viewModel.ChildLabel = user.FindFirst(TenantHierarchyConstants.ClaimTypes.ChildLabel)?.Value;
            viewModel.BackUrl = $"{parentAddress}/delegated-access/home";
            viewModel.SwitchUrl = $"{parentAddress}/delegated-access/switch";
            viewModel.EmbeddedPickerUrl = embedded
                ? $"{parentAddress}/{TenantHierarchyConstants.Routes.EmbeddedPicker}?child={Uri.EscapeDataString(_shellSettings.TenantId)}"
                : null;
            viewModel.UserName = user.Identity?.Name;
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:1");
    }
}
