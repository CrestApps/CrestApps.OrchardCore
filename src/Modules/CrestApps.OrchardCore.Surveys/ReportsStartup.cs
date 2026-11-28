using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// With Reports on, the survey reports under Reports → Surveys: results, responses, quiz results, item analysis,
/// completion funnel, participation, trend and activity.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<SurveyReportData>();
        services.AddDisplayDriver<ReportFilter, SurveyReportFilterDisplayDriver>();
        services
            .AddReport<SurveyResultsReport>()
            .AddReport<SurveyResponsesReport>()
            .AddReport<SurveyScoresReport>()
            .AddReport<SurveyItemAnalysisReport>()
            .AddReport<SurveyFunnelReport>()
            .AddReport<SurveyParticipationReport>()
            .AddReport<SurveyTrendReport>()
            .AddReport<SurveyActivityReport>();
    }
}
