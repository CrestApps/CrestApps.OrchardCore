using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Services;

public sealed class CustomChatAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public CustomChatAdminMenu(IStringLocalizer<CustomChatAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
           .Add(S["Artificial Intelligence"], artificialIntelligence =>
           {
               artificialIntelligence
                   .Add(S["Custom Chat"], "custom-chat", chat => chat
                       .Action("Index", "CustomChat", "CrestApps.OrchardCore.AI.Chat")
                       .Permission(AICustomChatPermissions.ManageOwnCustomChatInstances)
                       .LocalNav()
                   );
           });

        return ValueTask.CompletedTask;
    }
}
