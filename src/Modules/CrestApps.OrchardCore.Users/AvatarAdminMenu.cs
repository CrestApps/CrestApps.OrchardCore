using CrestApps.OrchardCore.Users.Drivers;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users;

public class AvatarAdminMenu : INavigationProvider
{
    protected readonly IStringLocalizer S;

    public AvatarAdminMenu(IStringLocalizer<UserDisplayNameAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public Task BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!NavigationHelper.IsAdminMenu(name))
        {
            return Task.CompletedTask;
        }

        builder
            .Add(S["Configuration"], configuration => configuration
                .Add(S["Settings"], settings => settings
                    .Add(S["User Avatars"], S["User Avatars"].PrefixPosition(), userAvatars => userAvatars
                        .AddClass("user-avatars")
                        .Id("userAvatars")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = UserAvatarOptionsDisplayDriver.GroupId })
                        .Permission(UserPermissions.ManageAvatarSettings)
                        .LocalNav()
                    )
                )
            );


        return Task.CompletedTask;
    }
}
