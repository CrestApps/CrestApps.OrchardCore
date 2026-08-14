using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Builds JSON schemas describing the rule conditions and operators available on the current tenant, used by
/// the <c>Layers</c> recipe step to describe each layer's <c>LayerRule</c>.
/// </summary>
public interface IRuleSchemaService
{
    /// <summary>
    /// Gets a descriptor for every rule condition contributed through an
    /// <see cref="IRuleConditionSchemaDefinition"/>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<RuleConditionDescriptor>> GetConditionDescriptorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a descriptor for every rule condition operator contributed through an
    /// <see cref="IRuleConditionOperatorSchemaDefinition"/>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<RuleConditionOperatorDescriptor>> GetOperatorDescriptorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema describing a condition operator object, as used by a condition's <c>Operation</c> member.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetOperatorSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the recursive schema describing a single entry of a rule's <c>Conditions</c> array. Condition
    /// groups nest additional conditions of the same shape.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetConditionSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema describing a layer's <c>LayerRule</c> object, including its recursive <c>Conditions</c> array.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetLayerRuleSchemaAsync(CancellationToken cancellationToken = default);
}
