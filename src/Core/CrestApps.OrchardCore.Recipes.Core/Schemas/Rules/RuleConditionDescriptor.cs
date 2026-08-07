using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

/// <summary>
/// Describes a rule condition that is available on the current tenant, along with the schema contributed for it.
/// </summary>
public sealed class RuleConditionDescriptor
{
    /// <summary>
    /// Gets the condition name as registered by its condition factory. This matches <c>Condition.Name</c>
    /// and the type name, for example <c>UrlCondition</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the polymorphic type discriminator serialized as the <c>$type</c> member, for example
    /// <c>OrchardCore.Rules.Models.UrlCondition, OrchardCore.Rules</c>.
    /// </summary>
    public required string TypeDiscriminator { get; init; }

    /// <summary>
    /// Gets the human readable condition title.
    /// </summary>
    public string DisplayText { get; init; }

    /// <summary>
    /// Gets a description explaining what the condition evaluates.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether the condition is a condition group that nests other conditions.
    /// </summary>
    public bool IsGroup { get; init; }

    /// <summary>
    /// Gets the property definitions that are specific to the condition, beyond the shared members.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; init; } = [];

    /// <summary>
    /// Gets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; init; } = [];
}
