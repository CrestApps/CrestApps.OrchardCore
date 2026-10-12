using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows;

/// <summary>
/// Starts the workflows that listen to the report events.
/// </summary>
public sealed class WorkflowReportEventHandler : IReportEventHandler
{
    private readonly IWorkflowManager _workflowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowReportEventHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager.</param>
    public WorkflowReportEventHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc/>
    public Task RanAsync(ReportEventContext context)
    {
        return TriggerAsync(nameof(ReportRunEvent), context);
    }

    /// <inheritdoc/>
    public Task ExportedAsync(ReportEventContext context)
    {
        return TriggerAsync(nameof(ReportExportedEvent), context);
    }

    /// <inheritdoc/>
    public Task PublishedAsync(ReportEventContext context)
    {
        return TriggerAsync(nameof(ReportPublishedEvent), context);
    }

    private Task TriggerAsync(string name, ReportEventContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Design is null)
        {
            return Task.CompletedTask;
        }

        var input = new Dictionary<string, object>
        {
            [ReportEventActivity.ReportIdInput] = context.Design.ItemId,
            ["ReportTitle"] = context.Design.DisplayText,
            ["UserName"] = context.UserName,
            ["Source"] = context.Source.ToString(),
            ["RowCount"] = context.RowCount,
            ["Format"] = context.Format,
            ["VersionNumber"] = context.VersionNumber,
            ["FilterValues"] = context.FilterValues,
        };

        return _workflowManager.TriggerEventAsync(name, input, correlationId: context.Design.ItemId);
    }
}
