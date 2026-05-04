using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Represents the chat interactions admin menu.
/// </summary>
public sealed class ChatInteractionsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatInteractionsAdminMenu(IStringLocalizer<ChatInteractionsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], artificialIntelligence =>
            {
                artificialIntelligence
                    .Add(S["Chat Interactions"], S["Chat Interactions"].PrefixPosition(), chatInteractions => chatInteractions
                        .Action("Index", "Admin", ChatInteractionsConstants.Feature.ChatInteractions)
                        .Permission(AIPermissions.ListChatInteractions)
                        .LocalNav()
                    );
            });

        return ValueTask.CompletedTask;
    }
}
