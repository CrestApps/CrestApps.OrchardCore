using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ValidateReCaptchaTask</c> workflow task.
/// </summary>
public sealed class ValidateReCaptchaTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ValidateReCaptchaTask";

    /// <inheritdoc />
    protected override string Category => "Validation";

    /// <inheritdoc />
    protected override string DisplayText => "Validate ReCaptcha Task";

    /// <inheritdoc />
    protected override string Description => "Validates the reCAPTCHA response token present in the current HTTP request";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Valid", "Invalid"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
        => [];
}
