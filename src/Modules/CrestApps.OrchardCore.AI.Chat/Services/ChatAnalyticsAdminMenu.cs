using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Services;

public sealed class ChatAnalyticsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ChatAnalyticsAdminMenu(IStringLocalizer<ChatAnalyticsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Chat Session Analytics"], S["Chat Session Analytics"].PrefixPosition(), analytics => analytics
                    .AddClass("chat-session-analytics")
                    .Id("chatSessionAnalytics")
                    .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                    .Action("Index", "ChatAnalytics", new RouteValueDictionary
                    {
                        { "area", "CrestApps.OrchardCore.AI.Chat" },
                    })
                    .LocalNav()
                ));

        return ValueTask.CompletedTask;
    }
}
