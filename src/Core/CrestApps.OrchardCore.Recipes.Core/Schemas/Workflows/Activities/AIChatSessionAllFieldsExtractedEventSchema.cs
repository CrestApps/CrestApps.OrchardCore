using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIChatSessionAllFieldsExtractedEvent</c> workflow event.
/// </summary>
public sealed class AIChatSessionAllFieldsExtractedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIChatSessionAllFieldsExtractedEvent";

    /// <inheritdoc />
    protected override string Category => "AI Chat";

    /// <inheritdoc />
    protected override string DisplayText => "AI Chat Session All Fields Extracted";

    /// <inheritdoc />
    protected override string Description => "Triggered when all fields have been extracted from an AI chat session";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("Optionally filter this event to a specific AI chat profile. Leave empty to trigger for any profile."));
    }
}
