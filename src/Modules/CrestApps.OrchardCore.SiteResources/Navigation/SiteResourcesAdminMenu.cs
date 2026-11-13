using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.SiteResources.Navigation;

/// <summary>
/// Adds Design › Site Resources to the admin menu, next to Templates.
/// </summary>
public sealed class SiteResourcesAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", SiteResourcesConstants.Features.SiteResources },
        { "controller", "SiteResources" },
        { "action", "Index" },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteResourcesAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SiteResourcesAdminMenu(IStringLocalizer<SiteResourcesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Design"], design => design
                .Add(S["Site Resources"], S["Site Resources"].PrefixPosition(), resources => resources
                    .Id("siteResources")
                    .Action(_routeValues)
                    .Permission(SiteResourcesPermissions.ManageSiteResources)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
