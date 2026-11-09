using CrestApps.OrchardCore.AI.Chat;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;

/// <summary>
/// The analytics features publish their pages as reports, which the Reports module lists, renders and exports, each
/// with the permission the page had.
/// </summary>
public sealed class AnalyticsReportsRegistrationTests
{
    [Fact]
    public void ChatAnalytics_RegistersTheSessionExtractedDataAndConversionReports()
    {
        var reports = Reports(new ChatAnalyticsUIStartup());

        Assert.Equal(
            [AIChatSessionAnalyticsReport.Name, AIChatExtractedDataReport.Name, AIChatConversionGoalsReport.Name],
            reports.Select(report => report.Name));
        Assert.All(reports, report => Assert.Equal("ViewChatAnalytics", report.Permission.Name));
    }

    [Fact]
    public void UsageAnalytics_RegistersTheUsageReport()
    {
        var report = Assert.Single(Reports(new UsageAnalyticsStartup()));

        Assert.Equal(AIUsageAnalyticsReport.Name, report.Name);
        Assert.Equal(ReportsConstants.Categories.BillingUsage, report.Category);
        Assert.Equal("ViewChatAnalytics", report.Permission.Name);
    }

    private static IReadOnlyList<ReportDescriptor> Reports(global::OrchardCore.Modules.StartupBase startup)
    {
        var services = new ServiceCollection();
        startup.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<ReportOptions>>().Value.Reports;
    }
}
