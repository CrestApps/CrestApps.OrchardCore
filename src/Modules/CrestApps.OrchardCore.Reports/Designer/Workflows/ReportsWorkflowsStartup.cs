using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;
using CrestApps.OrchardCore.Reports.Designer.Workflows.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows;

/// <summary>
/// Adds the report events and tasks to workflows, when the Workflows feature is on: a workflow can start when a person
/// runs, exports or publishes a report, and can run, export, or share a report.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ReportsWorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ReportWorkflowService>();
        services.AddScoped<IReportEventHandler, WorkflowReportEventHandler>();

        services.AddActivity<ReportRunEvent, ReportEventDisplayDriver<ReportRunEvent>>();
        services.AddActivity<ReportExportedEvent, ReportEventDisplayDriver<ReportExportedEvent>>();
        services.AddActivity<ReportPublishedEvent, ReportEventDisplayDriver<ReportPublishedEvent>>();

        services.AddActivity<RunReportTask, RunReportTaskDisplayDriver>();
        services.AddActivity<ExportReportTask, ExportReportTaskDisplayDriver>();
        services.AddActivity<CreateReportShareLinkTask, CreateReportShareLinkTaskDisplayDriver>();
    }
}

/// <summary>
/// Adds the Email Report task to workflows, when the Workflows and Email features are on.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures("OrchardCore.Workflows", "OrchardCore.Email")]
public sealed class ReportsEmailWorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<EmailReportTask, EmailReportTaskDisplayDriver>();
    }
}
