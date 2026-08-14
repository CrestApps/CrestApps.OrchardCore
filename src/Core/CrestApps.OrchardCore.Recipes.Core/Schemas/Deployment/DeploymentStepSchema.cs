using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;

/// <summary>
/// Describes the recipe payload of a single deployment step inside a plan's <c>Steps</c> array.
/// </summary>
public sealed class DeploymentStepSchema
{
    /// <summary>
    /// Gets or sets the human readable step title shown in the deployment plan editor.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the deployment step exports.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the property definitions accepted by the step's <c>Step</c> payload object.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the properties that must be provided in the step's <c>Step</c> payload.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether members beyond the declared ones are accepted in the
    /// <c>Step</c> payload.
    /// </summary>
    public bool AllowAdditionalProperties { get; set; } = true;
}
