using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users;

public sealed class UserDisplayNameAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", DisplayNameSettingsDisplayDriver.GroupId },
    };


    internal readonly IStringLocalizer S;

    public UserDisplayNameAdminMenu(IStringLocalizer<UserDisplayNameAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (OrchardCoreHelpers.UseLegacyAdminMenuFormat())
        {
            builder
                .Add(S["Configuration"], configuration => configuration
                    .Add(S["Settings"], settings => settings
                        .Add(S["User Display Name"], S["User Display Name"].PrefixPosition(), userDisplayName => userDisplayName
                            .AddClass("user-display-name")
                            .Id("userDisplayName")
                            .Action("Index", "Admin", _routeValues)
                            .Permission(UserPermissions.ManageDisplaySettings)
                            .LocalNav()
                        )
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["User Display Name"], S["User Display Name"].PrefixPosition(), userDisplayName => userDisplayName
                    .AddClass("user-display-name")
                    .Id("userDisplayName")
                    .Action("Index", "Admin", _routeValues)
                    .Permission(UserPermissions.ManageDisplaySettings)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
