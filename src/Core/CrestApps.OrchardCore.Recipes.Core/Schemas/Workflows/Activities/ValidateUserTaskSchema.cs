using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ValidateUserTask</c> workflow task.
/// </summary>
public sealed class ValidateUserTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ValidateUserTask";

    /// <inheritdoc />
    protected override string Category => "User";

    /// <inheritdoc />
    protected override string DisplayText => "Validate User Task";

    /// <inheritdoc />
    protected override string Description => "Validates the current HTTP request user and routes the workflow based on authentication status and role membership";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Anonymous", "Authenticated", "InRole"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("SetUserName", WorkflowActivitySchemaBuilders.Boolean("When true and the user is authenticated, stores the user's name in the 'UserName' workflow property. Defaults to true."));
        yield return ("Roles", WorkflowActivitySchemaBuilders.StringArray("The roles to check. The 'InRole' outcome is triggered if the authenticated user belongs to at least one of these roles.", context.Examples.RoleNames));
    }
}
