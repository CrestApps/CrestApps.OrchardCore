using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class AITemplateAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AITemplateAdminMenu(IStringLocalizer<AITemplateAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Templates"], S["Templates"].PrefixPosition(), templates => templates
                    .AddClass("ai-templates")
                    .Id("aiTemplates")
                    .Action("Index", "AITemplates", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIProfileTemplates)
                    .LocalNav()
                ));

        return ValueTask.CompletedTask;
    }
}
