using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ThemeDesigner;

/// <summary>
/// The permissions a theme draft is checked against. They are the permissions of the modules that own what the draft
/// changes, matched by name, so a designer can draft and publish only what they could change by hand.
/// </summary>
public static class ThemeDesignerPermissions
{
    /// <summary>
    /// Orchard Core's Templates permission: change the front-end shape templates.
    /// </summary>
    public static readonly Permission ManageTemplates = new("ManageTemplates", LocalizationSource.Create("Manage templates", typeof(ThemeDesignerPermissions)), isSecurityCritical: true);

    /// <summary>
    /// Orchard Core's Admin Templates permission: change the admin shape templates.
    /// </summary>
    public static readonly Permission ManageAdminTemplates = new("ManageAdminTemplates", LocalizationSource.Create("Manage admin templates", typeof(ThemeDesignerPermissions)), isSecurityCritical: true);
}
