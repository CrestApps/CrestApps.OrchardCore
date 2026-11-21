using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Surveys.Reports;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Defines the Survey Activity dashboard widget.
/// </summary>
internal sealed class SurveyReportWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SurveyReportWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Defines the widget.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.DefineReportWidgetAsync(
            "SurveyActivityWidget",
            "Survey Activity",
            "Completed responses per survey in the last days, and the responses waiting for a grader.",
            new ReportWidgetPartSettings
            {
                ReportName = SurveyReportNames.Activity,
                DefaultPeriod = ReportWidgetPeriod.Last7Days,
                DefaultContent = ReportWidgetContent.FiguresAndTables,
                DefaultMaxRows = 5,
            });

        return 1;
    }
}
