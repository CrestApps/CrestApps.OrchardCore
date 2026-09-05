using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

internal static class WorkflowOutcomeResults
{
    internal static ActivityExecutionResult From(string name)
        => new(new[] { name });
}
