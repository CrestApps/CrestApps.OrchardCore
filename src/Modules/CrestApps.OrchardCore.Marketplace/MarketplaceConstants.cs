using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Marketplace;

public static class MarketplaceConstants
{
    public const string Feature = "CrestApps.OrchardCore.Marketplace";

    /// <summary>
    /// The feature that pays vendors out to Stripe Connect accounts.
    /// </summary>
    public const string StripeConnectFeature = "CrestApps.OrchardCore.Marketplace.StripeConnect";

    /// <summary>
    /// The YesSql collection that holds vendors, their earnings and payouts.
    /// </summary>
    public const string CollectionName = "Marketplace";

    public const string SettingsGroupId = "marketplace";
}

public static class MarketplacePermissions
{
    /// <summary>
    /// Adds and changes vendors, releases their earnings and pays them out.
    /// </summary>
    public static readonly Permission ManageMarketplace = new("ManageMarketplace", "Manage the marketplace");
}

internal sealed class MarketplacePermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        MarketplacePermissions.ManageMarketplace,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
