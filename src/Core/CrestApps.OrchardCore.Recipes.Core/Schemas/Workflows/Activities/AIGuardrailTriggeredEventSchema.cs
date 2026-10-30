using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIGuardrailTriggeredEvent</c> workflow event.
/// </summary>
public sealed class AIGuardrailTriggeredEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIGuardrailTriggeredEvent";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "AI Guardrail Triggered";

    /// <inheritdoc />
    protected override string Description => "Triggered when a guardrail of an AI profile refused what the customer said or the AI's reply";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("Optionally run only for one AI profile."));
    }
}
