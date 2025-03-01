using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class AIConnectionsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AIConnectionsAdminMenu(IStringLocalizer<AIConnectionsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Connections"], S["Connections"].PrefixPosition(), connections => connections
                    .AddClass("openai-connections")
                    .Id("openaiConnection")
                    .Action("Index", "ProviderConnections", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageProviderConnections)
                    .LocalNav()
                ));

        return ValueTask.CompletedTask;
    }
}
