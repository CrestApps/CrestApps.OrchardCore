using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Settings › Commerce › Product Feeds.
/// </summary>
internal sealed class ProductFeedsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ProductFeedsAdminMenu(IStringLocalizer<ProductFeedsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Product Feeds"], S["Product Feeds"].PrefixPosition(), feeds => feeds
                        .Id("productFeedsSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = ProductFeedSettingsDisplayDriver.GroupId })
                        .Permission(ProductFeedPermissions.ManageProductFeeds)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
