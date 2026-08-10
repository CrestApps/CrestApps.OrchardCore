using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting;

/// <summary>
/// Describes the recipe payload of a single rewrite rule source inside the <c>Rules</c> array of the
/// <c>UrlRewriting</c> recipe step.
/// </summary>
public sealed class RewriteRuleSourceSchema
{
    /// <summary>
    /// Gets or sets the human readable source title shown in the rule editor.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the source does.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the property definitions that are specific to the source, beyond the shared members the
    /// schema service adds, such as <c>Id</c>, <c>Source</c>, <c>Name</c> and <c>Order</c>.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; set; } = [];
}
