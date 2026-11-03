using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UpdateContactMemoryTask</c> workflow activity.
/// </summary>
public sealed class UpdateContactMemoryTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UpdateContactMemoryTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Update Contact Memory";

    /// <inheritdoc />
    protected override string Description => "Rewrites what the AI remembers about a contact from a finished conversation";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Updated", "Nothing to remember"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("SessionId", WorkflowActivitySchemaBuilders.LiquidExpression("The AI session to remember. Defaults to {{ Workflow.Input.SessionId }}."));
        yield return ("ContactId", WorkflowActivitySchemaBuilders.LiquidExpression("Optionally, the contact's content item ID. Defaults to the contact the conversation was with."));
    }
}
