using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Tools;

public sealed class AIToolInstancesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AIToolInstancesAdminMenu(IStringLocalizer<AIToolInstancesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Tools"], tools => tools
                    .AddClass("ai-tools")
                    .Id("aiTools")
                    .Action("Index", "ToolInstances", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIToolInstances)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
