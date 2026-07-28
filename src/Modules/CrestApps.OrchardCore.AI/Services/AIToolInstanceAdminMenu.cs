using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Represents the AI tool instance admin menu.
/// </summary>
public sealed class AIToolInstanceAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolInstanceAdminMenu(IStringLocalizer<AIToolInstanceAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Tool Instances"], S["Tool Instances"].PrefixPosition(), instances => instances
                    .AddClass("ai-tool-instances")
                    .Id("aiToolInstances")
                    .Action("Index", "AIToolInstances", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIToolInstances)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
