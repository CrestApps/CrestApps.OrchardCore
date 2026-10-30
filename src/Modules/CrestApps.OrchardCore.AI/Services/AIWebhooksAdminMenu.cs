using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Adds the webhooks page to the Artificial Intelligence menu.
/// </summary>
public sealed class AIWebhooksAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhooksAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIWebhooksAdminMenu(IStringLocalizer<AIWebhooksAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Webhooks"], S["Webhooks"].PrefixPosition(), webhooks => webhooks
                    .AddClass("ai-webhooks")
                    .Id("aiWebhooks")
                    .Action("Index", "AIWebhooks", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIWebhooks)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
