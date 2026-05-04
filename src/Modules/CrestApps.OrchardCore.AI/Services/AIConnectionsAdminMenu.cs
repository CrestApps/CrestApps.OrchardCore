using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Represents the AI connections admin menu.
/// </summary>
public sealed class AIConnectionsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConnectionsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConnectionsAdminMenu(IStringLocalizer<AIConnectionsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Provider Connections"], S["Provider Connections"].PrefixPosition(), connections => connections
                    .AddClass("openai-connections")
                    .Id("openaiConnection")
                    .Action("Index", "ProviderConnections", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageProviderConnections)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
