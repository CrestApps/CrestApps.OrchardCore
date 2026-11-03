using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIConversationStartingEvent</c> workflow activity.
/// </summary>
public sealed class AIConversationStartingEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIConversationStartingEvent";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "AI Conversation Starting";

    /// <inheritdoc />
    protected override string Description => "Triggered before an AI conversation's first message on any channel; the conversation waits for it within the channel's time budget";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Channel", WorkflowActivitySchemaBuilders.String("Optionally run only for one channel: Phone, Sms, Chat or Email. Leave empty for every channel."));
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("Optionally run only for one AI profile. Leave empty for every profile."));
    }
}
