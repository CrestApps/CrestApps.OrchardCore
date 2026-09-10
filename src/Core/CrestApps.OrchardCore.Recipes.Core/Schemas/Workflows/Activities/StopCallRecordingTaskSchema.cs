using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>StopCallRecordingTask</c> workflow task.
/// </summary>
public sealed class StopCallRecordingTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StopCallRecordingTask";

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Stop Call Recording";

    /// <inheritdoc />
    protected override string Description => "Stops recording for a Contact Center interaction.";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Indeterminate", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("InteractionId", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the interaction identifier to stop recording."));
    }
}
