using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Adds the Canada LNNTE-DNCL Registry settings entry to the admin navigation menu.
/// </summary>
internal sealed class CanadaDnclRegistryAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", DncRegistryConstants.SettingsGroupIds.CanadaDnclRegistry },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CanadaDnclRegistryAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CanadaDnclRegistryAdminMenu(IStringLocalizer<CanadaDnclRegistryAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["DNC Registries"], S["DNC Registries"].PrefixPosition(), registries => registries
                    .AddClass("dnc-registries")
                    .Id("dncRegistries")
                    .Add(S["Canada LNNTE-DNCL Registry"], S["Canada LNNTE-DNCL Registry"].PrefixPosition(), canadaDncl => canadaDncl
                        .AddClass("canada-dncl-registry")
                        .Id("canadaDnclRegistry")
                        .Action("Index", "Admin", _routeValues)
                        .Permission(DncRegistryPermissions.ManageDncRegistrySettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
