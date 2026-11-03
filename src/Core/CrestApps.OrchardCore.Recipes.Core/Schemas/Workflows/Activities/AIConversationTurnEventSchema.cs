using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIConversationTurnEvent</c> workflow event.
/// </summary>
public sealed class AIConversationTurnEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIConversationTurnEvent";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "AI Conversation Turn";

    /// <inheritdoc />
    protected override string Description => "Triggered after each turn of an AI conversation is saved, while the conversation is going on";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Role", WorkflowActivitySchemaBuilders.String("Optionally run only for Customer or Assistant turns. Leave empty for both."));
        yield return ("Channel", WorkflowActivitySchemaBuilders.String("Optionally run only for one channel: Phone, Sms, Chat or Email."));
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("Optionally run only for one AI profile."));
    }
}
