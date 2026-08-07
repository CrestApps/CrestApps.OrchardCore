using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UserConfirmedEvent</c> workflow event.
/// </summary>
public sealed class UserConfirmedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UserConfirmedEvent";

    /// <inheritdoc />
    protected override string Category => "User";

    /// <inheritdoc />
    protected override string DisplayText => "User Confirmed Event";

    /// <inheritdoc />
    protected override string Description => "Triggers when a user confirms their email address";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("User", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that resolves to the User object. Inherited from the UserActivity base class; for events the user is provided via workflow input when the event fires rather than through this expression."));
    }
}
