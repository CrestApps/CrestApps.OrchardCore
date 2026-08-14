using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces the JSON schema and metadata that describe a single rule condition inside the <c>LayerRule</c>
/// of the <c>Layers</c> recipe step.
/// </summary>
/// <remarks>
/// Implement this interface when a module contributes a custom rule condition and wants the generated recipe
/// schema to describe the condition's members. Registering the implementation as
/// <see cref="IRuleConditionSchemaDefinition"/> is enough for the <c>Layers</c> recipe step to pick it up.
/// Prefer deriving from
/// <see cref="CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.RuleConditionSchemaDefinitionBase"/>, which
/// handles the standard condition envelope.
/// </remarks>
public interface IRuleConditionSchemaDefinition
{
    /// <summary>
    /// Gets the condition name that this definition describes. This must match <c>Condition.Name</c> and the
    /// condition type name exactly, for example <c>UrlCondition</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the polymorphic type discriminator serialized as the <c>$type</c> member, for example
    /// <c>OrchardCore.Rules.Models.UrlCondition, OrchardCore.Rules</c>.
    /// </summary>
    string TypeDiscriminator { get; }

    /// <summary>
    /// Builds the schema and metadata describing the condition.
    /// </summary>
    /// <param name="context">The context describing shared schema fragments the condition can reuse.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<RuleConditionSchema> GetConditionSchemaAsync(RuleConditionSchemaContext context, CancellationToken cancellationToken = default);
}
