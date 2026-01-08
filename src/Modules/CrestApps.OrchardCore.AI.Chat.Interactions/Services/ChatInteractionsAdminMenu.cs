using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

public sealed class ChatInteractionsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

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
                       .Action("Index", "Admin", AIConstants.Feature.ChatInteractions)
                       .Permission(AIPermissions.ListChatInteractions)
                       .LocalNav()
                   );
           });

        return ValueTask.CompletedTask;
    }
}
