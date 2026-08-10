using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIChatSessionPostProcessedEvent</c> workflow event.
/// </summary>
public sealed class AIChatSessionPostProcessedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIChatSessionPostProcessedEvent";

    /// <inheritdoc />
    protected override string Category => "AI Chat";

    /// <inheritdoc />
    protected override string DisplayText => "AI Chat Session Post-Processed";

    /// <inheritdoc />
    protected override string Description => "Triggered when an AI chat session has completed post-processing";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("Optionally filter this event to a specific AI chat profile. Leave empty to trigger for any profile."));
    }
}
