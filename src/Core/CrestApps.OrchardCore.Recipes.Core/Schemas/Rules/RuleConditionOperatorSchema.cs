using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

/// <summary>
/// Describes the recipe payload of a single rule condition operator used inside a condition's <c>Operation</c> member.
/// </summary>
public sealed class RuleConditionOperatorSchema
{
    /// <summary>
    /// Gets or sets the human readable operator title shown in the rule editor, for example <c>Starts with</c>.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the operator does.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the property definitions that are specific to the operator, beyond the shared
    /// <c>$type</c> member.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; set; } = [];
}
