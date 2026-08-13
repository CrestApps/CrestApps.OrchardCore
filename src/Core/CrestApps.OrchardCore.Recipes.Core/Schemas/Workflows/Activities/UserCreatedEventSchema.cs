using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UserCreatedEvent</c> workflow event.
/// </summary>
public sealed class UserCreatedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UserCreatedEvent";

    /// <inheritdoc />
    protected override string Category => "User";

    /// <inheritdoc />
    protected override string DisplayText => "User Created Event";

    /// <inheritdoc />
    protected override string Description => "Triggers when a new user account is created";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("User", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that resolves to the User object. Inherited from the UserActivity base class; for events the user is provided via workflow input when the event fires rather than through this expression."));
    }
}
