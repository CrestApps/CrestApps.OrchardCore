using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>DisableTenantTask</c> workflow task.
/// </summary>
public sealed class DisableTenantTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "DisableTenantTask";

    /// <inheritdoc />
    protected override string Category => "Tenant";

    /// <inheritdoc />
    protected override string DisplayText => "Disable Tenant Task";

    /// <inheritdoc />
    protected override string Description => "Disables a running tenant by its name";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Disabled", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("TenantName", WorkflowActivitySchemaBuilders.LiquidExpression("The name of the tenant to disable."));
    }
}
