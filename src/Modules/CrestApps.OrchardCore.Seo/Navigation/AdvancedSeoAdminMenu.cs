using CrestApps.OrchardCore.Seo.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Seo.Navigation;

/// <summary>
/// Settings › Search › Search Engine Optimization › Advanced SEO, beside Orchard Core's Robots settings.
/// </summary>
public sealed class AdvancedSeoAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", StructuredDataConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvancedSeoAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdvancedSeoAdminMenu(IStringLocalizer<AdvancedSeoAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Search"], S["Search"].PrefixPosition(), search => search
                    .Add(S["Search Engine Optimization"], S["Search Engine Optimization"].PrefixPosition(), seo => seo
                        .AddClass("seo")
                        .Id("seo")
                        .Add(S["Advanced SEO"], S["Advanced SEO"].PrefixPosition(), advancedSeo => advancedSeo
                            .Id("advancedSeoSettings")
                            .Action("Index", "Admin", _settingsRouteValues)
                            .Permission(AdvancedSeoPermissionsProvider.ManageAdvancedSeoSettings)
                            .LocalNav()
                        )
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
