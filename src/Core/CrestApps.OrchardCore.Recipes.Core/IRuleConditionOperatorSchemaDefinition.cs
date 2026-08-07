using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces the JSON schema and metadata that describe a single rule condition operator inside a condition's
/// <c>Operation</c> member of the <c>Layers</c> recipe step.
/// </summary>
/// <remarks>
/// Implement this interface when a module contributes a custom condition operator and wants the generated
/// recipe schema to describe the operator's members. Registering the implementation as
/// <see cref="IRuleConditionOperatorSchemaDefinition"/> is enough for the <c>Layers</c> recipe step to pick
/// it up. Prefer deriving from
/// <see cref="CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.RuleConditionOperatorSchemaDefinitionBase"/>.
/// </remarks>
public interface IRuleConditionOperatorSchemaDefinition
{
    /// <summary>
    /// Gets the operator name that this definition describes. This must match the operator type name exactly,
    /// for example <c>StringStartsWithOperator</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the polymorphic type discriminator serialized as the <c>$type</c> member, for example
    /// <c>OrchardCore.Rules.Models.StringStartsWithOperator, OrchardCore.Rules</c>.
    /// </summary>
    string TypeDiscriminator { get; }

    /// <summary>
    /// Builds the schema and metadata describing the operator.
    /// </summary>
    /// <param name="context">The context describing the operator being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<RuleConditionOperatorSchema> GetOperatorSchemaAsync(RuleConditionOperatorSchemaContext context, CancellationToken cancellationToken = default);
}
