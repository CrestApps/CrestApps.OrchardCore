using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ValidateAntiforgeryTokenTask</c> workflow task.
/// </summary>
public sealed class ValidateAntiforgeryTokenTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ValidateAntiforgeryTokenTask";

    /// <inheritdoc />
    protected override string Category => "Validation";

    /// <inheritdoc />
    protected override string DisplayText => "Validate Antiforgery Token Task";

    /// <inheritdoc />
    protected override string Description => "Validates the antiforgery token present in the current HTTP request";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Valid", "Invalid"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
        => [];
}
