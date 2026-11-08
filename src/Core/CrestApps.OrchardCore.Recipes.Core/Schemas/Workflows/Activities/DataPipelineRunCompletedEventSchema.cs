using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>DataPipelineRunCompletedEvent</c> workflow event.
/// </summary>
public sealed class DataPipelineRunCompletedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "DataPipelineRunCompletedEvent";

    /// <inheritdoc />
    protected override string Category => "Data";

    /// <inheritdoc />
    protected override string DisplayText => "Data Pipeline Run Completed Event";

    /// <inheritdoc />
    protected override string Description => "Starts or resumes a workflow when a run of a data pipeline ends, for example to notify someone when a run fails. The run is the last result";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PipelineId", WorkflowActivitySchemaBuilders.String("The identifier of the data pipeline whose runs trigger the event. When empty, the runs of every pipeline trigger it."));
        yield return ("Status", WorkflowActivitySchemaBuilders.String("The status of the runs that trigger the event. When empty, every run triggers it.", ["Succeeded", "Failed"]));
    }
}
