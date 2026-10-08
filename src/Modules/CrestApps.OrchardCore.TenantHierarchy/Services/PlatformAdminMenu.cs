using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Adds the tenant hierarchy screen to the Multi-Tenancy menu of the Default tenant.
/// </summary>
internal sealed class PlatformAdminMenu : AdminNavigationProvider
{
    private readonly ShellSettings _shellSettings;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformAdminMenu"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the Default tenant.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PlatformAdminMenu(
        ShellSettings shellSettings,
        IStringLocalizer<PlatformAdminMenu> stringLocalizer)
    {
        _shellSettings = shellSettings;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (!_shellSettings.IsDefaultShell())
        {
            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Multi-Tenancy"], "after.25", tenancy => tenancy
                .AddClass("menu-multitenancy")
                .Id("multitenancy")
                .Add(S["Tenant Hierarchy"], S["Tenant Hierarchy"].PrefixPosition(), hierarchy => hierarchy
                    .Action("Index", "Platform", TenantHierarchyConstants.Features.Area)
                    .Permission(TenantHierarchyPermissions.ManageTenantHierarchy)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
