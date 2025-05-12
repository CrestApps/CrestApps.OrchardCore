using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users;

public sealed class AvatarAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", UserAvatarOptionsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public AvatarAdminMenu(IStringLocalizer<UserDisplayNameAdminMenu> stringLocalizer)
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
                        .Add(S["User Avatars"], S["User Avatars"].PrefixPosition(), userAvatars => userAvatars
                            .AddClass("user-avatars")
                            .Id("userAvatars")
                            .Action("Index", "Admin", _routeValues)
                            .Permission(UserPermissions.ManageAvatarSettings)
                            .LocalNav()
                        )
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["User Avatars"], S["User Avatars"].PrefixPosition(), userAvatars => userAvatars
                    .AddClass("user-avatars")
                    .Id("userAvatars")
                    .Action("Index", "Admin", _routeValues)
                    .Permission(UserPermissions.ManageAvatarSettings)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
