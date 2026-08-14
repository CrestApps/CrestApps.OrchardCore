using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>EnableTenantTask</c> workflow task.
/// </summary>
public sealed class EnableTenantTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "EnableTenantTask";

    /// <inheritdoc />
    protected override string Category => "Tenant";

    /// <inheritdoc />
    protected override string DisplayText => "Enable Tenant Task";

    /// <inheritdoc />
    protected override string Description => "Enables a disabled tenant by its name";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Enabled", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("TenantName", WorkflowActivitySchemaBuilders.LiquidExpression("The name of the tenant to enable."));
    }
}
