using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UserLoggedOutEvent</c> workflow event.
/// </summary>
public sealed class UserLoggedOutEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UserLoggedOutEvent";

    /// <inheritdoc />
    protected override string Category => "User";

    /// <inheritdoc />
    protected override string DisplayText => "User Loggedout Event";

    /// <inheritdoc />
    protected override string Description => "Triggers when a user logs out. The 'UserName' and 'Roles' inputs hold the user who logged out";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("User", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that resolves to the User object. Inherited from the UserActivity base class; for events the user is provided via workflow input when the event fires rather than through this expression."));
    }
}
