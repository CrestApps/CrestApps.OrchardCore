using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>DeclineConversationTask</c> workflow activity.
/// </summary>
public sealed class DeclineConversationTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "DeclineConversationTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Decline Conversation";

    /// <inheritdoc />
    protected override string Description => "In an AI Conversation Starting workflow, stops the conversation from taking place";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Not starting"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Reason", WorkflowActivitySchemaBuilders.LiquidExpression("Optional. Why the conversation is declined, kept on the record."));
    }
}
