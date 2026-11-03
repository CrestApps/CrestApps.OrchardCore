using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIConversationStageTask</c> workflow activity.
/// </summary>
public sealed class AIConversationStageTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIConversationStageTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "AI Conversation Stage";

    /// <inheritdoc />
    protected override string Description => "A stage of a conversation flow: gives the AI its instructions and waits until one of its ways out applies";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Not in a conversation"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("StageName", WorkflowActivitySchemaBuilders.String("The stage's name."));
        yield return ("Instructions", WorkflowActivitySchemaBuilders.LiquidExpression("What the AI does in this stage."));
        yield return ("WaysOut", WorkflowActivitySchemaBuilders.String("The ways out, one 'outcome: when to take it' per line; each is an outcome of the activity."));
    }
}
