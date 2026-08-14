using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>BindModelStateTask</c> workflow task.
/// </summary>
public sealed class BindModelStateTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "BindModelStateTask";

    /// <inheritdoc />
    protected override string Category => "Validation";

    /// <inheritdoc />
    protected override string DisplayText => "Bind Model State Task";

    /// <inheritdoc />
    protected override string Description => "Binds all current HTTP form field values into the model state";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
