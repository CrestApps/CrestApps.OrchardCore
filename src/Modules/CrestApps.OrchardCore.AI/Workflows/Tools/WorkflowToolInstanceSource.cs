using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Workflows.Tools;

/// <summary>
/// A tool instance source whose instances each run a workflow: the AI calls the tool, the workflow runs with the
/// tool's arguments, and the workflow's output is the answer.
/// </summary>
internal sealed class WorkflowToolInstanceSource : IAIToolInstanceSource
{
    /// <inheritdoc/>
    public AITool CreateTool(AIToolInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var functionName = instance.GetFunctionName();

        return new WorkflowToolFunction(
            functionName,
            string.IsNullOrWhiteSpace(instance.Description) ? functionName : instance.Description,
            instance.GetOrCreate<WorkflowToolSettings>(),
            instance);
    }
}
