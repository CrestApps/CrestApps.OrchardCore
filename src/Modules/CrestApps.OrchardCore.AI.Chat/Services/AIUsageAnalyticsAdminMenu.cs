using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Adds the AI usage report to the Artificial Intelligence reports menu. It lives in the AI Chat feature rather than
/// the session analytics feature because the report works without it.
/// </summary>
public sealed class AIUsageAnalyticsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIUsageAnalyticsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIUsageAnalyticsAdminMenu(IStringLocalizer<AIUsageAnalyticsAdminMenu> stringLocalizer)
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
                    .Add(S["AI Usage Analytics"], S["AI Usage Analytics"].PrefixPosition(), usageAnalytics => usageAnalytics
                        .AddClass("ai-usage-analytics")
                        .Id("aiUsageAnalytics")
                        .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                        .Action("Index", "UsageAnalytics", "CrestApps.OrchardCore.AI.Chat")
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}