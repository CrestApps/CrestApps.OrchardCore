using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.SiteResources;

/// <summary>
/// The Site Resources permissions.
/// </summary>
public static class SiteResourcesPermissions
{
    /// <summary>
    /// Add, change and remove the stylesheets and scripts the site adds to its pages. A script runs in the browser of
    /// everyone who opens a page, so the permission is security critical.
    /// </summary>
    public static readonly Permission ManageSiteResources = new(
        "ManageSiteResources",
        LocalizationSource.Create("Manage site stylesheets and scripts", typeof(SiteResourcesPermissions)),
        isSecurityCritical: true);
}
