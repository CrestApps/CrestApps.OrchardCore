using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIConversationEvaluatedEvent</c> workflow event.
/// </summary>
public sealed class AIConversationEvaluatedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIConversationEvaluatedEvent";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "AI Conversation Evaluated";

    /// <inheritdoc />
    protected override string Description => "Triggered when an AI conversation, real or simulated, has been evaluated";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("Optionally run only for one AI profile."));
        yield return ("OnlyFailures", WorkflowActivitySchemaBuilders.Boolean("Run only for evaluations that failed."));
    }
}
