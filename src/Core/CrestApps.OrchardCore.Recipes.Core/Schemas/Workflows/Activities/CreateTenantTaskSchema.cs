using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>CreateTenantTask</c> workflow task.
/// </summary>
public sealed class CreateTenantTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "CreateTenantTask";

    /// <inheritdoc />
    protected override string Category => "Tenant";

    /// <inheritdoc />
    protected override string DisplayText => "Create Tenant Task";

    /// <inheritdoc />
    protected override string Description => "Creates a new tenant with the specified configuration settings";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("TenantName", WorkflowActivitySchemaBuilders.LiquidExpression("The name of the tenant to create."));
        yield return ("Description", WorkflowActivitySchemaBuilders.LiquidExpression("The optional description of the tenant to create."));
        yield return ("RequestUrlPrefix", WorkflowActivitySchemaBuilders.LiquidExpression("The optional URL prefix for the tenant, for example 'site1' in 'http://orchardproject.net/site1'."));
        yield return ("RequestUrlHost", WorkflowActivitySchemaBuilders.LiquidExpression("The optional host name for the tenant, for example 'orchardproject.net' in 'http://orchardproject.net/'."));
        yield return ("DatabaseProvider", WorkflowActivitySchemaBuilders.LiquidExpression("The database provider to use for the tenant, for example 'SqlConnection' or 'Sqlite'."));
        yield return ("ConnectionString", WorkflowActivitySchemaBuilders.LiquidExpression("The database connection string for the tenant."));
        yield return ("TablePrefix", WorkflowActivitySchemaBuilders.LiquidExpression("The database table prefix for the tenant."));
        yield return ("Schema", WorkflowActivitySchemaBuilders.LiquidExpression("The database schema for the tenant. When left blank, the default value on the server is used (for example 'dbo' for SQL Server)."));
        yield return ("RecipeName", WorkflowActivitySchemaBuilders.LiquidExpression("The name of the setup recipe to associate with the tenant."));
        yield return ("FeatureProfile", WorkflowActivitySchemaBuilders.LiquidExpression("The optional feature profile to apply to the tenant."));
    }
}
