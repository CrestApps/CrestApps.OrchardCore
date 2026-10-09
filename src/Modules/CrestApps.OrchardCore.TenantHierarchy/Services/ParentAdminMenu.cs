using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Adds the child tenants admin of a parent to the admin menu, under the words the parent policy sets.
/// </summary>
internal sealed class ParentAdminMenu : AdminNavigationProvider
{
    // The Audit Trail's own permission is not public; permissions are matched by name.
    private static readonly Permission _viewAuditTrail = new("ViewAuditTrail", "View Audit Trail");

    private readonly ShellSettings _shellSettings;
    private readonly HierarchyLabelsProvider _labelsProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentAdminMenu"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the parent tenant.</param>
    /// <param name="labelsProvider">The labels provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ParentAdminMenu(
        ShellSettings shellSettings,
        HierarchyLabelsProvider labelsProvider,
        IStringLocalizer<ParentAdminMenu> stringLocalizer)
    {
        _shellSettings = shellSettings;
        _labelsProvider = labelsProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (!_shellSettings.IsParentTenant())
        {
            return ValueTask.CompletedTask;
        }

        var labels = _labelsProvider.GetLabels();
        var children = new LocalizedString("ChildTenants", labels.Children);

        builder
            .Add(children, "after.5", group => group
                .AddClass("child-tenants")
                .AddClass("icon-class-fa-solid")
                .AddClass("icon-class-fa-sitemap")
                .Id("childTenants")
                .Add(new LocalizedString("ChildTenantsList", S["All {0}", labels.ChildrenLower]), "1", list => list
                    .Action("Index", "ChildTenants", TenantHierarchyConstants.Features.Area)
                    .Permission(TenantHierarchyPermissions.ViewChildTenants)
                    .LocalNav())
                .Add(new LocalizedString("ChildTenantsSwitch", S["Open a {0}", labels.ChildLower]), "2", open => open
                    .Action("Switch", "ChildTenants", TenantHierarchyConstants.Features.Area)
                    .Permission(TenantHierarchyPermissions.EnterChildTenants)
                    .LocalNav())
                .Add(S["Access"], "3", access => access
                    .Action("Index", "Access", TenantHierarchyConstants.Features.Area)
                    .Permission(TenantHierarchyPermissions.ManageChildAccess)
                    .LocalNav())
                // The activity is recorded in the audit trail; this opens it filtered to the tenant hierarchy events.
                .Add(S["Activity"], "4", activity => activity
                    .Action("Index", "Admin", "OrchardCore.AuditTrail", new RouteValueDictionary { ["q"] = $"category:{HierarchyAuditEventNames.Category}" })
                    .Permission(_viewAuditTrail)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
