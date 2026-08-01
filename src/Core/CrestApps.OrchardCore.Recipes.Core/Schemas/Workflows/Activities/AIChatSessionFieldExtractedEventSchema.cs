using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIChatSessionFieldExtractedEvent</c> workflow event.
/// </summary>
public sealed class AIChatSessionFieldExtractedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIChatSessionFieldExtractedEvent";

    /// <inheritdoc />
    protected override string Category => "AI Chat";

    /// <inheritdoc />
    protected override string DisplayText => "AI Chat Session Field Extracted";

    /// <inheritdoc />
    protected override string Description => "Triggered when a field is extracted from an AI chat session";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("Optionally filter this event to a specific AI chat profile. Leave empty to trigger for any profile."));
    }
}
