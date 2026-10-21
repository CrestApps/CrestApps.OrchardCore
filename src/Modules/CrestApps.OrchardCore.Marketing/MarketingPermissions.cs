using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Marketing;

public static class MarketingPermissions
{
    /// <summary>
    /// Adds and changes segments and campaigns, and sends campaigns.
    /// </summary>
    public static readonly Permission ManageMarketing = new(
        "ManageMarketing",
        LocalizationSource.Create("Manage marketing", typeof(MarketingPermissions)));
}
