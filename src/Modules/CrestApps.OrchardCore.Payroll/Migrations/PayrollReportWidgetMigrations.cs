using CrestApps.OrchardCore.Payroll.Reports;
using CrestApps.OrchardCore.Reports;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Defines Payroll's report widgets.
/// </summary>
public sealed class PayrollReportWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollReportWidgetMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definitions.</param>
    public PayrollReportWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Defines the widgets.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.DefineReportWidgetAsync(
            "PayrollCashRequirementWidget",
            "Payroll Cash Requirement",
            "The cash the pay runs of the coming days need in the bank.",
            new ReportWidgetPartSettings
            {
                ReportName = CashRequirementReport.Name,
                DefaultPeriod = ReportWidgetPeriod.Last30Days,
                DefaultContent = ReportWidgetContent.FiguresAndTables,
                DefaultMaxRows = 5,
            });

        await _contentDefinitionManager.DefineReportWidgetAsync(
            "PayrollSummaryWidget",
            "Payroll Summary",
            "The total of each earning, deduction, tax and contribution of the last pay.",
            new ReportWidgetPartSettings
            {
                ReportName = PayrollSummaryReport.Name,
                DefaultPeriod = ReportWidgetPeriod.Last30Days,
                DefaultContent = ReportWidgetContent.TablesOnly,
                DefaultMaxRows = 8,
            });

        return 1;
    }
}
