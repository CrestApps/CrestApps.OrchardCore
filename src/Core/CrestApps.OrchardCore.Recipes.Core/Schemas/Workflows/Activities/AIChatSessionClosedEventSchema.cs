using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AIChatSessionClosedEvent</c> workflow event.
/// </summary>
public sealed class AIChatSessionClosedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AIChatSessionClosedEvent";

    /// <inheritdoc />
    protected override string Category => "AI Chat";

    /// <inheritdoc />
    protected override string DisplayText => "AI Chat Session Closed";

    /// <inheritdoc />
    protected override string Description => "Triggered when an AI chat session is closed";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("Optionally filter this event to a specific AI chat profile. Leave empty to trigger for any profile."));
    }
}
