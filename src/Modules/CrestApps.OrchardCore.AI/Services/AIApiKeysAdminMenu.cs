using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Adds the API keys page to the Artificial Intelligence menu.
/// </summary>
public sealed class AIApiKeysAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIApiKeysAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIApiKeysAdminMenu(IStringLocalizer<AIApiKeysAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["API keys"], S["API keys"].PrefixPosition(), keys => keys
                    .AddClass("ai-api-keys")
                    .Id("aiApiKeys")
                    .Action("Index", "AIApiKeys", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIApiKeys)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
