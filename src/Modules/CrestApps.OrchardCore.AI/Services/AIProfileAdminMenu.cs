using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Represents the AI profile admin menu.
/// </summary>
public sealed class AIProfileAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileAdminMenu(IStringLocalizer<AIProfileAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], "90", ai => ai
                .AddClass("artificial-intelligence")
                .Id("artificialIntelligence")
                .Add(S["Profiles"], S["Profiles"].PrefixPosition(), profiles => profiles
                    .AddClass("ai-profiles")
                    .Id("aiProfiles")
                    .Action("Index", "Profiles", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIProfiles)
                    .LocalNav()
                ),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
