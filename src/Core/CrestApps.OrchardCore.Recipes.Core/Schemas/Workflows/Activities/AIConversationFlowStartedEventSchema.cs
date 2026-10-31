using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIConversationFlowStartedEvent</c> workflow activity.
/// </summary>
public sealed class AIConversationFlowStartedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIConversationFlowStartedEvent";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "AI Conversation Flow Started";

    /// <inheritdoc />
    protected override string Description => "Runs when a conversation of a profile that uses this workflow as its conversation flow takes its first turn";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield break;
    }
}
