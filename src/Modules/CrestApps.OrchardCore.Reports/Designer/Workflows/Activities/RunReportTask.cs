using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;

/// <summary>
/// Runs a report with its owner's access and writes the result to the workflow output: the report, the number of rows,
/// the column labels, and the rows by column label, for the tasks that follow, such as a script or an email.
/// </summary>
public sealed class RunReportTask : ReportTaskActivity<RunReportTask>
{
    /// <summary>
    /// The output name used when none is set.
    /// </summary>
    public const string DefaultOutputName = "Report";

    private readonly ReportWorkflowService _reports;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunReportTask"/> class.
    /// </summary>
    /// <param name="reports">The report workflow service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RunReportTask(
        ReportWorkflowService reports,
        ILogger<RunReportTask> logger,
        IStringLocalizer<RunReportTask> stringLocalizer)
        : base(stringLocalizer)
    {
        _reports = reports;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Run Report"];

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["No rows"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var design = await _reports.FindReportAsync(this, workflowContext);

        if (design is null)
        {
            _logger.LogWarning("The Run Report task of workflow '{WorkflowId}' found no report to run.", workflowContext.WorkflowId);

            return Outcome("Failed");
        }

        var run = await _reports.RunAsync(design, await _reports.GetFilterValuesAsync(Filters, workflowContext));
        var output = ReportWorkflowService.ToOutput(design, run);

        workflowContext.Output[string.IsNullOrWhiteSpace(OutputName) ? DefaultOutputName : OutputName.Trim()] = output;
        workflowContext.LastResult = output;

        if (run.Errors.Count > 0)
        {
            _logger.LogWarning("The Run Report task of workflow '{WorkflowId}' could not run report '{ReportId}'.", workflowContext.WorkflowId, design.ItemId);

            return Outcome("Failed");
        }

        return Outcome(run.RowCount == 0 ? "No rows" : "Done");
    }
}
