using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ValidateFormTask</c> workflow task.
/// </summary>
public sealed class ValidateFormTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ValidateFormTask";

    /// <inheritdoc />
    protected override string Category => "Validation";

    /// <inheritdoc />
    protected override string DisplayText => "Validate Form Task";

    /// <inheritdoc />
    protected override string Description => "Checks whether the current model state contains any validation errors";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Valid", "Invalid"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
