using CrestApps.OrchardCore.Reports;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Shipping.Migrations;

/// <summary>
/// Defines the shipping widget.
/// </summary>
public sealed class ShippingReportWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ShippingReportWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.DefineReportWidgetAsync(
            "ShippingTimesWidget",
            "Shipping Times",
            "How long orders waited from payment to shipping in the period, by carrier, and the slowest to ship.",
            new ReportWidgetPartSettings
            {
                ReportName = "shipping-performance",
                DefaultPeriod = ReportWidgetPeriod.Last30Days,
                DefaultContent = ReportWidgetContent.FiguresAndTables,
                DefaultMaxRows = 5,
            });

        return 1;
    }
}
