using CrestApps.OrchardCore.Reports;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Orders.Dashboard.Migrations;

/// <summary>
/// Defines the order widgets.
/// </summary>
public sealed class OrderReportWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public OrderReportWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await Define("SalesOverviewWidget", "Sales Overview", "What the orders paid in the period came to: orders, sales, discounts, shipping, tax and refunds.", "order-sales", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresOnly);
        await Define("SalesTrendWidget", "Sales Trend", "The sales of the period, day by day (month by month for a long period), as a chart.", "order-sales", ReportWidgetPeriod.Last30Days, ReportWidgetContent.ChartsOnly);
        await Define("TopProductsWidget", "Top Selling Products", "The products that sold the most units in the period.", "order-product-sales", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresOnly, 5);
        await Define("CategorySalesWidget", "Sales by Category", "What each category of products sold in the period.", "order-category-sales", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresOnly, 5);
        await Define("CountrySalesWidget", "Sales by Country", "The countries the orders of the period shipped to, and what they came to.", "order-country-sales", ReportWidgetPeriod.Last30Days, ReportWidgetContent.TablesOnly, 5);
        await Define("OrdersToShipWidget", "Orders to Ship", "The paid orders that haven't shipped in full, oldest first.", "unfulfilled-orders", ReportWidgetPeriod.AllTime, ReportWidgetContent.FiguresAndTables, 5);
        await Define("CustomerInsightsWidget", "Customer Insights", "First-time and returning buyers of the period, and the biggest buyers.", "order-customers", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresAndTables, 5);
        await Define("CheckoutFunnelWidget", "Checkout Conversion", "The checkouts started in the period and how they ended: paid, failed, abandoned.", "checkout-conversion", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresOnly);
        await Define("RefundsWidget", "Refunds", "The money given back in the period.", "order-refunds", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresAndTables, 5);
        await Define("DisputesWidget", "Payment Disputes", "The payments disputed with the payer's bank in the period, open ones first.", "order-disputes", ReportWidgetPeriod.Last90Days, ReportWidgetContent.FiguresAndTables, 5);
        await Define("DiscountCodesWidget", "Discount Codes Used", "How many orders used each promotion code in the period, and what it took off.", "order-discounts", ReportWidgetPeriod.Last30Days, ReportWidgetContent.FiguresAndTables, 5);
        await Define("TaxCollectedWidget", "Tax Collected", "The tax charged on the orders paid in the period.", "order-tax", ReportWidgetPeriod.ThisMonth, ReportWidgetContent.FiguresOnly);

        return 1;
    }

    private Task Define(string type, string name, string description, string report, ReportWidgetPeriod period, ReportWidgetContent content, int maxRows = 5)
        => _contentDefinitionManager.DefineReportWidgetAsync(type, name, description, new ReportWidgetPartSettings
        {
            ReportName = report,
            DefaultPeriod = period,
            DefaultContent = content,
            DefaultMaxRows = maxRows,
        });
}
