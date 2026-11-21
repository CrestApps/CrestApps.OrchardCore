using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Memberships.Navigation;

/// <summary>
/// Settings › Commerce › Memberships.
/// </summary>
public sealed class MembershipSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", MembershipsConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public MembershipSettingsAdminMenu(IStringLocalizer<MembershipSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Memberships"], S["Memberships"].PrefixPosition(), memberships => memberships
                        .Id("membershipsSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(MembershipsPermissions.ManageMemberships)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
