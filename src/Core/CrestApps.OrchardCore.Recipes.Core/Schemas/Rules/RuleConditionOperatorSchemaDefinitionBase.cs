using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

/// <summary>
/// Provides the standard implementation surface for rule condition operator schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe a condition operator in the <c>Layers</c> recipe step. Implementations
/// only supply the operator name, its type discriminator and the members it accepts; the schema service
/// assembles the shared <c>$type</c> member.
/// </remarks>
public abstract class RuleConditionOperatorSchemaDefinitionBase : IRuleConditionOperatorSchemaDefinition
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string TypeDiscriminator { get; }

    /// <summary>
    /// Gets the human readable operator title. Returns <see langword="null"/> when no title is provided.
    /// </summary>
    protected virtual string DisplayText => null;

    /// <summary>
    /// Gets a description explaining what the operator does.
    /// </summary>
    protected virtual string Description => null;

    ValueTask<RuleConditionOperatorSchema> IRuleConditionOperatorSchemaDefinition.GetOperatorSchemaAsync(
        RuleConditionOperatorSchemaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildOperatorSchemaAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds the schema describing the operator. Override this method when the schema requires asynchronous work.
    /// </summary>
    /// <param name="context">The context describing the operator being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected virtual ValueTask<RuleConditionOperatorSchema> BuildOperatorSchemaAsync(
        RuleConditionOperatorSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildOperatorSchemaCore(context));

    /// <summary>
    /// Builds the property definitions accepted by the operator, beyond the shared <c>$type</c> member.
    /// </summary>
    protected virtual IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
        => [];

    /// <summary>
    /// Assembles the operator schema from the declared metadata and property definitions.
    /// </summary>
    /// <param name="context">The context describing the operator being documented.</param>
    protected virtual RuleConditionOperatorSchema BuildOperatorSchemaCore(RuleConditionOperatorSchemaContext context)
    {
        var properties = GetPropertyDefinitions()?.ToArray() ?? [];

        return new RuleConditionOperatorSchema
        {
            DisplayText = DisplayText,
            Description = Description,
            Properties = properties,
        };
    }
}
