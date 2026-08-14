using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UnassignUserRoleTask</c> workflow task.
/// </summary>
public sealed class UnassignUserRoleTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UnassignUserRoleTask";

    /// <inheritdoc />
    protected override string Category => "User";

    /// <inheritdoc />
    protected override string DisplayText => "Unassign User Role Task";

    /// <inheritdoc />
    protected override string Description => "Removes one or more roles from a specified user";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["UserName", "Roles"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("UserName", WorkflowActivitySchemaBuilders.LiquidExpression("The username of the user from whom to remove the roles."));
        yield return ("Roles", WorkflowActivitySchemaBuilders.StringArray("The roles to remove from the user.", context.Examples.RoleNames));
    }
}
