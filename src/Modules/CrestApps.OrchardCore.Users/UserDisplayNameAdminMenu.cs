using CrestApps.OrchardCore.Users.Drivers;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users;

public sealed class UserDisplayNameAdminMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public UserDisplayNameAdminMenu(IStringLocalizer<UserDisplayNameAdminMenu> stringLocalizer)
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
                    .Add(S["User Display Name"], S["User Display Name"].PrefixPosition(), userDisplayName => userDisplayName
                        .AddClass("user-display-name")
                        .Id("userDisplayName")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = DisplayNameSettingsDisplayDriver.GroupId })
                        .Permission(UserPermissions.ManageDisplaySettings)
                        .LocalNav()
                    )
                )
            );


        return Task.CompletedTask;
    }
}
