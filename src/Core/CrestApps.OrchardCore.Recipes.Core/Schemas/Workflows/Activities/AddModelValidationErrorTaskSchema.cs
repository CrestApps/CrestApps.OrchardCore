using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AddModelValidationErrorTask</c> workflow task.
/// </summary>
public sealed class AddModelValidationErrorTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AddModelValidationErrorTask";

    /// <inheritdoc />
    protected override string Category => "Validation";

    /// <inheritdoc />
    protected override string DisplayText => "Add Model Validation Error Task";

    /// <inheritdoc />
    protected override string Description => "Adds a model validation error to the current model state for a specified form field";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Key", WorkflowActivitySchemaBuilders.String("The name of the form field that has an invalid value."));
        yield return ("ErrorMessage", WorkflowActivitySchemaBuilders.String("The validation error message to display."));
    }
}
