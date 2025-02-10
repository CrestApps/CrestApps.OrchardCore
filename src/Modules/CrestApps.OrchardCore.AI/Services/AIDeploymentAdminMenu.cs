using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class AIDeploymentAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AIDeploymentAdminMenu(IStringLocalizer<AIDeploymentAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Deployments"], "after.10", deployments => deployments
                    .AddClass("ai-deployments")
                    .Id("aiDeployments")
                    .Action("Index", "Deployments", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageModelDeployments)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
