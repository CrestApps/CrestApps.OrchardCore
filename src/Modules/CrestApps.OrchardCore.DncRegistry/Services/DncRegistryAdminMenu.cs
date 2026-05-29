using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Adds the DNC Registry settings to the admin navigation menu under Settings.
/// </summary>
internal sealed class DncRegistryAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _importContentRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", DncRegistryConstants.SettingsGroupIds.ImportContentSettings },
    };

    private static readonly RouteValueDictionary _usaFtcRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", DncRegistryConstants.SettingsGroupIds.UsaFtcRegistry },
    };

    private static readonly RouteValueDictionary _canadaDnclRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", DncRegistryConstants.SettingsGroupIds.CanadaDnclRegistry },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DncRegistryAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DncRegistryAdminMenu(IStringLocalizer<DncRegistryAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Import Content"], S["Import Content"].PrefixPosition(), importContent => importContent
                    .AddClass("import-content-settings")
                    .Id("importContentSettings")
                    .Action("Index", "Admin", _importContentRouteValues)
                    .Permission(DncRegistryPermissions.ManageDncRegistrySettings)
                    .LocalNav()
                )
                .Add(S["DNC Registries"], S["DNC Registries"].PrefixPosition(), registries => registries
                    .AddClass("dnc-registries")
                    .Id("dncRegistries")
                    .Add(S["USA FTC Registry"], S["USA FTC Registry"].PrefixPosition(), usaFtc => usaFtc
                        .AddClass("usa-ftc-registry")
                        .Id("usaFtcRegistry")
                        .Action("Index", "Admin", _usaFtcRouteValues)
                        .Permission(DncRegistryPermissions.ManageDncRegistrySettings)
                        .LocalNav()
                    )
                    .Add(S["Canada LNNTE-DNCL Registry"], S["Canada LNNTE-DNCL Registry"].PrefixPosition(), canadaDncl => canadaDncl
                        .AddClass("canada-dncl-registry")
                        .Id("canadaDnclRegistry")
                        .Action("Index", "Admin", _canadaDnclRouteValues)
                        .Permission(DncRegistryPermissions.ManageDncRegistrySettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
