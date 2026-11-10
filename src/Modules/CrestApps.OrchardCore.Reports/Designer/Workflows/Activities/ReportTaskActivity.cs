using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;

/// <summary>
/// A workflow task that uses a designed report: the report is chosen, or resolved by a Liquid expression, and its
/// filters can be set, one <c>filter id = value</c> per line.
/// </summary>
/// <typeparam name="TActivity">The task type.</typeparam>
public abstract class ReportTaskActivity<TActivity> : TaskActivity<TActivity>, IReportWorkflowTask
    where TActivity : ITask
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected ReportTaskActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <summary>
    /// Gets the name of the task in the telemetry: <c>run</c>, <c>export</c>, <c>email</c> or <c>share_link</c>.
    /// </summary>
    protected abstract string TelemetryName { get; }

    /// <summary>
    /// Ends the task with an outcome, and counts the run in <see cref="ReportTelemetry.WorkflowTasks"/>.
    /// </summary>
    /// <param name="outcome">The name of the outcome, such as <c>Done</c> or <c>Failed</c>.</param>
    /// <returns>The result of the task.</returns>
    protected ActivityExecutionResult Finish(string outcome)
    {
        ReportTelemetry.Count(
            ReportTelemetry.WorkflowTasks,
            (ReportTelemetry.WorkflowTaskTag, TelemetryName),
            (CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome == "Failed" ? CrestAppsOrchardCoreTelemetry.OutcomeError : CrestAppsOrchardCoreTelemetry.OutcomeOk));

        return Outcome(outcome);
    }

    /// <inheritdoc/>
    public override LocalizedString Category => S["Reports"];

    /// <inheritdoc/>
    public string ReportId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public string ReportIdExpression
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public string Filters
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the name of the workflow output the task writes, read in Liquid as <c>Workflow.Output.Name</c>.
    /// </summary>
    public string OutputName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
}
