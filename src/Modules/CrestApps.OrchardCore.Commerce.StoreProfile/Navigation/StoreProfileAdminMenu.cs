using CrestApps.OrchardCore.Commerce.StoreProfile.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Commerce.StoreProfile.Navigation;

/// <summary>
/// Settings › Commerce › Store.
/// </summary>
public sealed class StoreProfileAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", StoreProfileSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public StoreProfileAdminMenu(IStringLocalizer<StoreProfileAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Store"], S["Store"].PrefixPosition(), store => store
                        .Id("storeProfileSettings")
                        .Action("Index", "Admin", _routeValues)
                        .Permission(StoreProfilePermissions.ManageStoreProfile)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
