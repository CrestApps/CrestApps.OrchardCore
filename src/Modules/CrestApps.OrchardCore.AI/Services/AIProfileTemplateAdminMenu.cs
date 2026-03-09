using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class AIProfileTemplateAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AIProfileTemplateAdminMenu(IStringLocalizer<AIProfileTemplateAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Profile Templates"], S["Profile Templates"].PrefixPosition(), templates => templates
                    .AddClass("ai-profile-templates")
                    .Id("aiProfileTemplates")
                    .Action("Index", "ProfileTemplates", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIProfileTemplates)
                    .LocalNav()
                ));

        return ValueTask.CompletedTask;
    }
}
