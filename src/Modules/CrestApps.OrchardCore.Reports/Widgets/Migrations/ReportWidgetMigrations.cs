using CrestApps.OrchardCore.Reports;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Reports.Widgets.Migrations;

/// <summary>
/// Defines the part that shows a report and the dashboard widget that shows any report.
/// </summary>
internal sealed class ReportWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ReportWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(ReportWidgetConstants.PartName, part => part
            .WithDisplayName("Report Widget")
            .WithDescription("Shows a report on the admin dashboard for a period that moves with the clock, and keeps the result for a while."));

        await _contentDefinitionManager.DefineReportWidgetAsync(
            ReportWidgetConstants.GenericWidgetType,
            "Report",
            "Any report you may view, on the admin dashboard: pick the report, its period and what to show.",
            new ReportWidgetPartSettings());

        return 1;
    }
}
