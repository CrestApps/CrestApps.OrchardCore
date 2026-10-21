using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Marketplace;

public static class MarketplacePermissions
{
    /// <summary>
    /// Adds and changes vendors, releases their earnings and pays them out.
    /// </summary>
    public static readonly Permission ManageMarketplace = new(
        "ManageMarketplace",
        LocalizationSource.Create("Manage the marketplace", typeof(MarketplacePermissions)),
        isSecurityCritical: true);
}
