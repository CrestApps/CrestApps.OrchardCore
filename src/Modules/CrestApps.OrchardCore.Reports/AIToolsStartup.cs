using CrestApps.Core.AI;
using CrestApps.OrchardCore.Reports.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Registers the AI tools that list, describe, run and export reports whenever the AI feature is on. One set of tools
/// serves every built-in report and, when the Report Builder is on, every designed report.
/// </summary>
[Feature(ReportsConstants.Feature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<ReportToolScopeChanges>();

        services.AddCoreAITool<ListReportsTool>(ListReportsTool.TheName)
            .WithTitle(S["List Reports"])
            .WithDescription(S["Lists the reports the user may open: built-in reports with their category and description, and, when the Report Builder is on, designed reports with their group and owner. Supports a text search and a category or group filter. Use it first to find the report that answers a question."])
            .WithCategory(S["Reports"])
            .Selectable();

        services.AddCoreAITool<GetReportTool>(GetReportTool.TheName)
            .WithTitle(S["Get Report"])
            .WithDescription(S["Describes one report: what it shows, the filters it accepts with their allowed values, its default period, the export formats and whether the user may export. For a designed report it can also return the full definition, its saved versions and its share links."])
            .WithCategory(S["Reports"])
            .Selectable();

        services.AddCoreAITool<RunReportTool>(RunReportTool.TheName)
            .WithTitle(S["Run Report"])
            .WithDescription(S["Runs a report for a period and filter values and returns its headline numbers and its tables as columns and rows, capped at a row limit with the total row count. Use it to answer questions from a report; use exportReport when the user wants a file."])
            .WithCategory(S["Reports"])
            .Selectable();

        services.AddCoreAITool<ExportReportTool>(ExportReportTool.TheName)
            .WithTitle(S["Export Report"])
            .WithDescription(S["Exports a report for a period and filter values as CSV, Excel or PDF and returns a download link with the file name. Excel and PDF need their features to be enabled."])
            .WithCategory(S["Reports"])
            .Selectable();
    }
}
