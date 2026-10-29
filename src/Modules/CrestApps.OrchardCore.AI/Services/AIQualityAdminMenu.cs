using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Adds the AI quality pages to the Artificial Intelligence menu.
/// </summary>
public sealed class AIQualityAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQualityAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIQualityAdminMenu(IStringLocalizer<AIQualityAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Tests"], S["Tests"].PrefixPosition(), tests => tests
                    .AddClass("ai-tests")
                    .Id("aiTests")
                    .Action("Index", "AITestCases", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIQuality)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
