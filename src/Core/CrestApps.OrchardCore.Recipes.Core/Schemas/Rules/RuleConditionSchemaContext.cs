using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

/// <summary>
/// Provides contextual information shared with a rule condition definition while its recipe schema is built.
/// </summary>
public sealed class RuleConditionSchemaContext
{
    /// <summary>
    /// Gets the schema describing a condition operator object, composed from every registered
    /// <see cref="IRuleConditionOperatorSchemaDefinition"/>. Conditions that expose an <c>Operation</c>
    /// property use this schema to describe it.
    /// </summary>
    public required JsonSchemaBuilder OperatorSchema { get; init; }

    /// <summary>
    /// Gets the example values from the current tenant that conditions surface as non-restrictive suggestions.
    /// </summary>
    public RecipeSchemaExamples Examples { get; init; } = RecipeSchemaExamples.Empty;
}
