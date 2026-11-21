using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts Surveys workflow events in a deferred task, after the change that raised them is committed.
/// </summary>
internal sealed class DeferredSurveyWorkflowTrigger : ISurveyWorkflowTrigger
{
    /// <inheritdoc />
    public void Trigger(string eventName, IDictionary<string, object> input, string correlationId)
        => ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(eventName, input, correlationId));
}
