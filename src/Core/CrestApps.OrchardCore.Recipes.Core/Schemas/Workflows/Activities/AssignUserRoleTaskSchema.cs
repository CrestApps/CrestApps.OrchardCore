using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AssignUserRoleTask</c> workflow task.
/// </summary>
public sealed class AssignUserRoleTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AssignUserRoleTask";

    /// <inheritdoc />
    protected override string Category => "User";

    /// <inheritdoc />
    protected override string DisplayText => "Assign User Role Task";

    /// <inheritdoc />
    protected override string Description => "Assigns a role to a specified user";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["UserName", "RoleName"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("UserName", WorkflowActivitySchemaBuilders.LiquidExpression("The username of the user to assign the role to."));
        yield return ("RoleName", WorkflowActivitySchemaBuilders.LiquidExpression("The name of the role to assign to the user."));
    }
}
