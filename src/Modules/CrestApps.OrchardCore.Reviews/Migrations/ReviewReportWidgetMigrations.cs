using CrestApps.OrchardCore.Reports;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Reviews.Migrations;

/// <summary>
/// Defines the review widget.
/// </summary>
public sealed class ReviewReportWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ReviewReportWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.DefineReportWidgetAsync(
            "ProductReviewsWidget",
            "Product Reviews",
            "The reviews written in the period: how many wait for approval, the stars given, and the products reviewed most.",
            new ReportWidgetPartSettings
            {
                ReportName = "product-reviews",
                DefaultPeriod = ReportWidgetPeriod.Last30Days,
                DefaultContent = ReportWidgetContent.FiguresAndTables,
                DefaultMaxRows = 5,
            });

        return 1;
    }
}
