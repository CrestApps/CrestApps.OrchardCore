using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ValidateFormFieldTask</c> workflow task.
/// </summary>
public sealed class ValidateFormFieldTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ValidateFormFieldTask";

    /// <inheritdoc />
    protected override string Category => "Validation";

    /// <inheritdoc />
    protected override string DisplayText => "Validate Form Field Task";

    /// <inheritdoc />
    protected override string Description => "Validates that a specific form field is not empty";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Valid", "Invalid"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["FieldName", "ErrorMessage"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("FieldName", WorkflowActivitySchemaBuilders.String("The name of the form field to validate."));
        yield return ("ErrorMessage", WorkflowActivitySchemaBuilders.String("The validation error message to display when the field is empty."));
    }
}
