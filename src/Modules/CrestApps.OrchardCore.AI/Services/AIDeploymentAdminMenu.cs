using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Represents the AI deployment admin menu.
/// </summary>
public sealed class AIDeploymentAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDeploymentAdminMenu(IStringLocalizer<AIDeploymentAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Deployments"], S["Deployments"].PrefixPosition(), deployments => deployments
                    .AddClass("ai-deployments")
                    .Id("aiDeployments")
                    .Action("Index", "Deployments", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIDeployments)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
