using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

/// <summary>
/// Describes a rule condition operator that is available on the current tenant, along with the schema contributed for it.
/// </summary>
public sealed class RuleConditionOperatorDescriptor
{
    /// <summary>
    /// Gets the operator name as registered by its operator factory. This matches the operator type name,
    /// for example <c>StringStartsWithOperator</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the polymorphic type discriminator serialized as the <c>$type</c> member, for example
    /// <c>OrchardCore.Rules.Models.StringStartsWithOperator, OrchardCore.Rules</c>.
    /// </summary>
    public required string TypeDiscriminator { get; init; }

    /// <summary>
    /// Gets the human readable operator title.
    /// </summary>
    public string DisplayText { get; init; }

    /// <summary>
    /// Gets a description explaining what the operator does.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the property definitions that are specific to the operator, beyond the shared <c>$type</c> member.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; init; } = [];
}
