using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UserUpdatedEvent</c> workflow event.
/// </summary>
public sealed class UserUpdatedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UserUpdatedEvent";

    /// <inheritdoc />
    protected override string Category => "User";

    /// <inheritdoc />
    protected override string DisplayText => "User Updated Event";

    /// <inheritdoc />
    protected override string Description => "Triggers when a user account is updated";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("User", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that resolves to the User object. Inherited from the UserActivity base class; for events the user is provided via workflow input when the event fires rather than through this expression."));
    }
}
