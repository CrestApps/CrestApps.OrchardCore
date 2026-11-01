using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// The <see cref="IWorkforceEvents"/> used with the Workflows feature: each event starts the workflows that begin
/// with its activity.
/// </summary>
public sealed class WorkflowWorkforceEvents : IWorkforceEvents
{
    private readonly IWorkflowManager _workflowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowWorkforceEvents"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager.</param>
    public WorkflowWorkforceEvents(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc />
    public Task RaiseAsync(string name, IDictionary<string, object> input, string correlationId, CancellationToken cancellationToken = default)
    {
        return _workflowManager.TriggerEventAsync(name, input, correlationId);
    }
}
