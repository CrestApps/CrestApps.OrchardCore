using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SetupTenantTask</c> workflow task.
/// </summary>
public sealed class SetupTenantTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SetupTenantTask";

    /// <inheritdoc />
    protected override string Category => "Tenant";

    /// <inheritdoc />
    protected override string DisplayText => "Setup Tenant Task";

    /// <inheritdoc />
    protected override string Description => "Runs the setup process for an uninitialized tenant";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("TenantName", WorkflowActivitySchemaBuilders.LiquidExpression("The name of the tenant to set up."));
        yield return ("SiteName", WorkflowActivitySchemaBuilders.LiquidExpression("The display name for the site."));
        yield return ("AdminUsername", WorkflowActivitySchemaBuilders.LiquidExpression("The user name of the initial administrator account."));
        yield return ("AdminEmail", WorkflowActivitySchemaBuilders.LiquidExpression("The email address of the initial administrator account."));
        yield return ("AdminPassword", WorkflowActivitySchemaBuilders.LiquidExpression("The password for the initial administrator account."));
        yield return ("DatabaseProvider", WorkflowActivitySchemaBuilders.LiquidExpression("The database provider to use. Falls back to the tenant shell setting when left blank."));
        yield return ("DatabaseConnectionString", WorkflowActivitySchemaBuilders.LiquidExpression("The database connection string. Falls back to the tenant shell setting when left blank."));
        yield return ("DatabaseTablePrefix", WorkflowActivitySchemaBuilders.LiquidExpression("The database table prefix. Falls back to the tenant shell setting when left blank."));
        yield return ("DatabaseSchema", WorkflowActivitySchemaBuilders.LiquidExpression("The database schema. Falls back to the tenant shell setting when left blank. For example, 'dbo' for SQL Server."));
        yield return ("RecipeName", WorkflowActivitySchemaBuilders.LiquidExpression("The name of the setup recipe to run. Falls back to the tenant shell setting when left blank."));
    }
}
