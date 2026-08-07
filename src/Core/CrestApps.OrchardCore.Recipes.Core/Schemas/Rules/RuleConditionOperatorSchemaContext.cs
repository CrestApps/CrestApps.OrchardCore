namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

/// <summary>
/// Provides contextual information shared with a rule condition operator definition while its recipe schema is built.
/// </summary>
public sealed class RuleConditionOperatorSchemaContext
{
    /// <summary>
    /// Gets the operator name as registered by its operator factory. This matches the operator type name,
    /// for example <c>StringStartsWithOperator</c>.
    /// </summary>
    public required string OperatorName { get; init; }
}
