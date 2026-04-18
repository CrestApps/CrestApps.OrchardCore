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
                .Add(S["Reports"], S["Reports"].PrefixPosition(), reports => reports
                    .AddClass("ai-reports")
                    .Id("aiReports")
                    .Add(S["AI Chat Session Analytics"], S["AI Chat Session Analytics"].PrefixPosition(), analytics => analytics
                        .AddClass("chat-session-analytics")
                        .Id("chatSessionAnalytics")
                        .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                        .Action("Index", "ChatAnalytics", "CrestApps.OrchardCore.AI.Chat")
                        .LocalNav()
                    )
                    .Add(S["AI Chat Extracted Data"], S["AI Chat Extracted Data"].PrefixPosition(), extractedData => extractedData
                        .AddClass("chat-extracted-data")
                        .Id("chatExtractedData")
                        .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                        .Action("Index", "ChatExtractedData", "CrestApps.OrchardCore.AI.Chat")
                        .LocalNav()
                    )
                    .Add(S["AI Usage Analytics"], S["AI Usage Analytics"].PrefixPosition(), usageAnalytics => usageAnalytics
                        .AddClass("ai-usage-analytics")
                        .Id("aiUsageAnalytics")
                        .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                        .Action("Index", "UsageAnalytics", "CrestApps.OrchardCore.AI.Chat")
                        .LocalNav()
                    )
                    .Add(S["AI Chat Conversion Goals"], S["AI Chat Conversion Goals"].PrefixPosition(), conversionGoals => conversionGoals
                        .AddClass("chat-conversion-goals")
                        .Id("chatConversionGoals")
                        .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                        .Action("Index", "ChatConversionGoals", "CrestApps.OrchardCore.AI.Chat")
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
