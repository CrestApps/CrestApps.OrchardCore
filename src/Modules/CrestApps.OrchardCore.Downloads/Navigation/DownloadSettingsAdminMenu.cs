using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Downloads.Navigation;

public sealed class DownloadSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", DownloadsConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public DownloadSettingsAdminMenu(IStringLocalizer<DownloadSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Downloads"], S["Downloads"].PrefixPosition(), downloads => downloads
                        .Id("downloadsSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(DownloadsPermissions.ManageDownloads)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
