using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

/// <summary>
/// Describes the recipe payload of a single rule condition used inside a layer rule.
/// </summary>
public sealed class RuleConditionSchema
{
    /// <summary>
    /// Gets or sets the human readable condition title shown in the rule editor.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the condition evaluates.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the condition is a condition group that nests other
    /// conditions inside its own <c>Conditions</c> array, such as <c>AllConditionGroup</c> or
    /// <c>AnyConditionGroup</c>.
    /// </summary>
    public bool IsGroup { get; set; }

    /// <summary>
    /// Gets or sets the property definitions that are specific to the condition, beyond the shared
    /// <c>$type</c>, <c>Name</c> and <c>ConditionId</c> members. The recursive <c>Conditions</c> array of a
    /// group condition is added by the schema service and must not be returned here.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; set; } = [];
}
