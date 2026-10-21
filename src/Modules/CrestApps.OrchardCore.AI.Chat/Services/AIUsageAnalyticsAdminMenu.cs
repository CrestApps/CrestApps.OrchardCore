using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Lists the AI usage report under the Billing &amp; Usage category of the top-level Reports menu. It lives in the AI
/// Chat feature rather than the session analytics feature because the report works without it.
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
        // The top-level Reports menu is built by the Reports module, which merges this entry into its category by name.
        // The merged node may keep this entry's id and classes, so they match the Reports module's; without the
        // "reports" id the menu loses its icon, which is rendered by the NavigationItemText-reports.Id shape.
        var category = new LocalizedString(ReportsConstants.Categories.BillingUsage, ReportsConstants.Categories.BillingUsage);

        builder
            .Add(S["Reports"], "after.40", reports => reports
                .AddClass("reports")
                .Id("reports")
                .Add(category, category.PrefixPosition(), categoryNode => categoryNode
                    .AddClass("report-category")
                    .Add(S["AI Usage Analytics"], S["AI Usage Analytics"].PrefixPosition(), usageAnalytics => usageAnalytics
                        .AddClass("report")
                        .Id("aiUsageAnalytics")
                        .Permission(ChatAnalyticsPermissionProvider.ViewChatAnalytics)
                        .Action("Index", "UsageAnalytics", "CrestApps.OrchardCore.AI.Chat")
                        .LocalNav()
                    )
                ), priority: 1);

        return ValueTask.CompletedTask;
    }
}