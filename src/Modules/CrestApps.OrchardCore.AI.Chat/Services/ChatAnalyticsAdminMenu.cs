using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Represents the chat analytics admin menu.
/// </summary>
public sealed class ChatAnalyticsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatAnalyticsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatAnalyticsAdminMenu(IStringLocalizer<ChatAnalyticsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        // The reports live under Reports > Artificial Intelligence Reports. The top-level Reports menu is built by the
        // Reports module, which merges this entry into its category by name; the merged node may keep this entry's id
        // and classes, so they match the Reports module's (without the "reports" id the menu loses its icon).
        var category = new LocalizedString(ReportsConstants.Categories.AIReports, ReportsConstants.Categories.AIReports);

        builder
            .Add(S["Reports"], "after.40", reports => reports
                .AddClass("reports")
                .Id("reports")
                .Add(category, category.PrefixPosition(), categoryNode => categoryNode
                    .AddClass("report-category")
                    .Add(S["AI Chat Session Analytics"], S["AI Chat Session Analytics"].PrefixPosition(), analytics => analytics
                        .AddClass("report")
                        .Id("chatSessionAnalytics")
                        .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                        .Action("Index", "ChatAnalytics", "CrestApps.OrchardCore.AI.Chat")
                        .LocalNav()
                    )
                    .Add(S["AI Chat Extracted Data"], S["AI Chat Extracted Data"].PrefixPosition(), extractedData => extractedData
                        .AddClass("report")
                        .Id("chatExtractedData")
                        .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                        .Action("Index", "ChatExtractedData", "CrestApps.OrchardCore.AI.Chat")
                        .LocalNav()
                    )
                    .Add(S["AI Chat Conversion Goals"], S["AI Chat Conversion Goals"].PrefixPosition(), conversionGoals => conversionGoals
                        .AddClass("report")
                        .Id("chatConversionGoals")
                        .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                        .Action("Index", "ChatConversionGoals", "CrestApps.OrchardCore.AI.Chat")
                        .LocalNav()
                    )
                ), priority: 1);

        return ValueTask.CompletedTask;
    }
}
