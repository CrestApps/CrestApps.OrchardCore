using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>RunDataPipelineTask</c> workflow task.
/// </summary>
public sealed class RunDataPipelineTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "RunDataPipelineTask";

    /// <inheritdoc />
    protected override string Category => "Data";

    /// <inheritdoc />
    protected override string DisplayText => "Run Data Pipeline Task";

    /// <inheritdoc />
    protected override string Description => "Queues a run of the published version of a data pipeline. When it waits for the run, the workflow halts and resumes once the run ends, with the run in the 'DataPipelineRun' output and as the last result";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Succeeded", "Queued", "Failed", "Skipped"];

    /// <inheritdoc />
    protected override bool HasDynamicOutcomes => true;

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["PipelineId"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PipelineId", WorkflowActivitySchemaBuilders.String("The identifier of the data pipeline to run."));
        yield return ("WaitForCompletion", WorkflowActivitySchemaBuilders.Boolean("Whether the workflow waits for the run to end. Defaults to true. When true, the outcomes are 'Succeeded' and 'Failed'; otherwise they are 'Queued' and 'Failed'."));
        yield return ("SkipWhenActive", WorkflowActivitySchemaBuilders.Boolean("Whether the task does not queue a run while one of the pipeline is queued or running, and takes the 'Skipped' outcome instead. Defaults to false."));
        yield return ("Parameters", WorkflowActivitySchemaBuilders.LiquidExpression("The parameters of the run, one 'name=value' per line. Steps read them with the '{Parameter:name}' placeholder."));
    }
}
