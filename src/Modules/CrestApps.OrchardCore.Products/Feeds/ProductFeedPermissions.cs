using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.Feeds;

public static class ProductFeedPermissions
{
    public static readonly Permission ManageProductFeeds = new(
        "ManageProductFeeds",
        LocalizationSource.Create("Manage product feeds", typeof(ProductFeedPermissions)));
}
