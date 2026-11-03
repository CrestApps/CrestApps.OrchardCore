using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.NLWeb.Navigation;

/// <summary>
/// Settings › Search › NLWeb.
/// </summary>
public sealed class NLWebAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", NLWebConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NLWebAdminMenu(IStringLocalizer<NLWebAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Search"], S["Search"].PrefixPosition(), search => search
                    .Add(S["NLWeb"], S["NLWeb"].PrefixPosition(), nlweb => nlweb
                        .Id("nlwebSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(NLWebPermissionsProvider.ManageNLWebSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
