using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Adds AI personas to the Artificial Intelligence admin menu.
/// </summary>
public sealed class AIPersonaAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIPersonaAdminMenu(IStringLocalizer<AIPersonaAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Personas"], S["Personas"].PrefixPosition(), personas => personas
                    .AddClass("ai-personas")
                    .Id("aiPersonas")
                    .Action("Index", "AIPersonas", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIPersonas)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
