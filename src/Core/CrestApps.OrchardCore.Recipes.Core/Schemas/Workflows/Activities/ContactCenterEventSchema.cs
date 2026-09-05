using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ContactCenterEvent</c> workflow event.
/// </summary>
public sealed class ContactCenterEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ContactCenterEvent";

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Contact Center Event";

    /// <inheritdoc />
    protected override string Description => "Starts or resumes a workflow when a Contact Center domain event is published.";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Matched", "Ignored"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("EventType", WorkflowActivitySchemaBuilders.String("The domain event type this activity reacts to. When empty, it reacts to every event."));
    }
}
