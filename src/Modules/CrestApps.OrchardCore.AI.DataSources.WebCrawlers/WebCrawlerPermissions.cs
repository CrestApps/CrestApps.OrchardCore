using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers;

/// <summary>
/// Permissions provided by the Web Crawlers feature.
/// </summary>
public static class WebCrawlerPermissions
{
    /// <summary>
    /// Permission that allows managing web crawlers (create, edit, delete, and synchronize).
    /// </summary>
    public static readonly Permission ManageWebCrawlers = new("ManageWebCrawlers", "Manage web crawlers");
}
