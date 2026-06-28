using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Telephony.Navigation;

/// <summary>
/// Adds the telephony settings entry to the admin navigation.
/// </summary>
public sealed class TelephonyAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", TelephonyConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelephonyAdminMenu(IStringLocalizer<TelephonyAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Communication"], S["Communication"].PrefixPosition(), communication => communication
                    .Add(S["Telephony"], S["Telephony"].PrefixPosition(), telephony => telephony
                        .AddClass("telephony")
                        .Id("telephony")
                        .Action("Index", "Admin", _routeValues)
                        .Permission(TelephonyPermissions.ManageTelephonySettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
