using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Adds the USA FTC Registry settings entry to the admin navigation menu.
/// </summary>
internal sealed class UsaFtcDncRegistryAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", DncRegistryConstants.SettingsGroupIds.UsaFtcRegistry },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsaFtcDncRegistryAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UsaFtcDncRegistryAdminMenu(IStringLocalizer<UsaFtcDncRegistryAdminMenu> stringLocalizer)
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
                    .Add(S["USA FTC Registry"], S["USA FTC Registry"].PrefixPosition(), usaFtc => usaFtc
                        .AddClass("usa-ftc-registry")
                        .Id("usaFtcRegistry")
                        .Action("Index", "Admin", _routeValues)
                        .Permission(DncRegistryPermissions.ManageDncRegistrySettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
