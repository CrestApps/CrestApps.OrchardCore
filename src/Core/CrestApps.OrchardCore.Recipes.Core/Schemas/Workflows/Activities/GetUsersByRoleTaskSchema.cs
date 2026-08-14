using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>GetUsersByRoleTask</c> workflow task.
/// </summary>
public sealed class GetUsersByRoleTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "GetUsersByRoleTask";

    /// <inheritdoc />
    protected override string Category => "User";

    /// <inheritdoc />
    protected override string DisplayText => "Get Users by Role Task";

    /// <inheritdoc />
    protected override string Description => "Retrieves users belonging to one or more specified roles and stores the result in the workflow output";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["OutputKeyName", "Roles"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("OutputKeyName", WorkflowActivitySchemaBuilders.LiquidExpression("The key name used to store the retrieved users dictionary in the workflow output, enabling access to the list later in the workflow."));
        yield return ("Roles", WorkflowActivitySchemaBuilders.StringArray("The roles used to identify users. All users that belong to at least one of the specified roles are included in the result.", context.Examples.RoleNames));
    }
}
