using CrestApps.OrchardCore.Reports;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Downloads.Migrations;

/// <summary>
/// Defines the downloads widget.
/// </summary>
public sealed class DownloadsReportWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public DownloadsReportWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.DefineReportWidgetAsync(
            "DigitalDownloadsWidget",
            "Digital Downloads",
            "The download links given in the period, by product: how many were used and how many never were.",
            new ReportWidgetPartSettings
            {
                ReportName = "digital-downloads",
                DefaultPeriod = ReportWidgetPeriod.Last30Days,
                DefaultContent = ReportWidgetContent.FiguresAndTables,
                DefaultMaxRows = 5,
            });

        return 1;
    }
}
