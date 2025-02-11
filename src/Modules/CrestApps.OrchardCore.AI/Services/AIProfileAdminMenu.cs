using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class AIProfileAdminMenu : AdminNavigationProvider
{

    internal readonly IStringLocalizer S;

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
                .Add(S["Profiles"], "after.5", profiles => profiles
                    .AddClass("ai-profiles")
                    .Id("aiProfiles")
                    .Action("Index", "Profiles", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIProfiles)
                    .LocalNav()
                )
            , priority: 1);

        return ValueTask.CompletedTask;
    }
}

