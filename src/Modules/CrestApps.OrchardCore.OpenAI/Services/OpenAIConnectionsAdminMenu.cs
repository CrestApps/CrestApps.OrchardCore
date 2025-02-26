using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.OpenAI.Services;

public sealed class OpenAIConnectionsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public OpenAIConnectionsAdminMenu(IStringLocalizer<OpenAIConnectionsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["OpenAI Connections"], S["OpenAI Connections"].PrefixPosition(), connections => connections
                    .AddClass("openai-connections")
                    .Id("openaiConnection")
                    .Action("Index", "Admin", OpenAIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageOpenAIConnections)
                    .LocalNav()
                ));

        return ValueTask.CompletedTask;
    }
}
