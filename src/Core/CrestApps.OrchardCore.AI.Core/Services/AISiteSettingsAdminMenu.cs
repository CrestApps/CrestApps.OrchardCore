using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class AISiteSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", AIConstants.AISettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public AISiteSettingsAdminMenu(IStringLocalizer<AISiteSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
           .Add(S["Settings"], settings =>
           {
               settings
                   .Add(S["Artificial Intelligence"], S["Artificial Intelligence"].PrefixPosition(), ai => ai
                       .Action("Index", "Admin", _routeValues)
                       .Permission(AIPermissions.ManageAIProfiles)
                       .LocalNav()
                   );
           });

        return ValueTask.CompletedTask;
    }
}
