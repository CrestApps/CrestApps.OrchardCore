using CrestApps.OrchardCore.Reports;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Defines the subscription widgets.
/// </summary>
public sealed class SubscriptionReportWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SubscriptionReportWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await Define("SubscriptionsOverviewWidget", "Subscriptions Overview", "Active, new and expiring subscriptions, and the total number.", "subscriptions-dashboard", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresOnly);
        await Define("SubscriptionRevenueWidget", "Subscription Revenue", "Gross revenue, transaction volume and average transaction value of the period.", "subscription-revenue", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresOnly);
        await Define("ExpiringSubscriptionsWidget", "Expiring Subscriptions", "The subscriptions due to expire soon, nearest first.", "expiring-subscriptions", ReportWidgetPeriod.AllTime, ReportWidgetContent.TablesOnly);
        await Define("NewSubscriptionsTrendWidget", "New Subscriptions Trend", "The new subscriptions started each month, as a chart.", "new-subscriptions-trend", ReportWidgetPeriod.Last12Months, ReportWidgetContent.ChartsOnly);
        await Define("SubscriptionProductsWidget", "Subscription Products", "Revenue and transactions of each subscription product in the period.", "product-performance", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresAndTables);

        return 1;
    }

    private Task Define(string type, string name, string description, string report, ReportWidgetPeriod period, ReportWidgetContent content)
        => _contentDefinitionManager.DefineReportWidgetAsync(type, name, description, new ReportWidgetPartSettings
        {
            ReportName = report,
            DefaultPeriod = period,
            DefaultContent = content,
            DefaultMaxRows = 5,
        });
}
