using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UseAIProfileTask</c> workflow activity.
/// </summary>
public sealed class UseAIProfileTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UseAIProfileTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Use AI Profile";

    /// <inheritdoc />
    protected override string Description => "In an AI Conversation Starting workflow, answers the conversation with a different AI profile";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Not starting"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["ProfileId"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("The identifier of the AI profile to answer with."));
    }
}
