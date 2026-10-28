using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Represents the AI template admin menu.
/// </summary>
public sealed class AITemplateAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AITemplateAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
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
                )
                .Add(S["Speaking Styles"], S["Speaking Styles"].PrefixPosition(), styles => styles
                    .AddClass("ai-speaking-styles")
                    .Id("aiSpeakingStyles")
                    .Action("Index", "AITemplates", new RouteValueDictionary
                    {
                        { "area", AIConstants.Feature.Area },
                        { "category", AISpeakingStyles.Category },
                    })
                    .Permission(AIPermissions.ManageAIProfileTemplates)
                    .LocalNav()
                )
                .Add(S["Guardrail Topics"], S["Guardrail Topics"].PrefixPosition(), topics => topics
                    .AddClass("ai-guardrail-topics")
                    .Id("aiGuardrailTopics")
                    .Action("Index", "AITemplates", new RouteValueDictionary
                    {
                        { "area", AIConstants.Feature.Area },
                        { "category", AIGuardrailTopics.Category },
                    })
                    .Permission(AIPermissions.ManageAIProfileTemplates)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
