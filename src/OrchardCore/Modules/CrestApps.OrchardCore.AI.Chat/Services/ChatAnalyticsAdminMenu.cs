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
                    .Action("Index", "ChatAnalytics", "CrestApps.OrchardCore.AI.Chat").LocalNav()
            )
            .Add(S["Chat Extracted Data"], S["Chat Extracted Data"].PrefixPosition(), extractedData => extractedData
                .AddClass("chat-extracted-data")
                .Id("chatExtractedData")
                .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                .Action("Index", "ChatExtractedData", "CrestApps.OrchardCore.AI.Chat")
                .LocalNav()
            )
            .Add(S["Chat Conversion Goals"], S["Chat Conversion Goals"].PrefixPosition(), conversionGoals => conversionGoals
                .AddClass("chat-conversion-goals")
                .Id("chatConversionGoals")
                .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                .Action("Index", "ChatConversionGoals", "CrestApps.OrchardCore.AI.Chat")
                .LocalNav()
          )
       );

        return ValueTask.CompletedTask;
    }
}
