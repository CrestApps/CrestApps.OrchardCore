using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ThemeDesigner.Navigation;

/// <summary>
/// Adds Design › Theme Draft to the admin menu.
/// </summary>
public sealed class ThemeDesignerAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", ThemeDesignerConstants.Features.ThemeDesigner },
        { "controller", "ThemeDesigner" },
        { "action", "Index" },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeDesignerAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ThemeDesignerAdminMenu(IStringLocalizer<ThemeDesignerAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Design"], design => design
                .Add(S["Theme Draft"], S["Theme Draft"].PrefixPosition(), draft => draft
                    .Id("themeDraft")
                    .Action(_routeValues)
                    .Permission(ThemeDesignerPermissions.ManageTemplates)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
