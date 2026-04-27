using CrestApps.OrchardCore.AI.Core;
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
            );

        return ValueTask.CompletedTask;
    }
}
