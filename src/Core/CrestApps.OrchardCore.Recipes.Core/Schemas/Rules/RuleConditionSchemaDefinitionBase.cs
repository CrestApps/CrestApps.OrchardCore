using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;

/// <summary>
/// Provides the standard implementation surface for rule condition schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe a rule condition in the <c>Layers</c> recipe step. Implementations only
/// supply the condition name, its type discriminator and the members it accepts; the schema service assembles
/// the shared <c>$type</c>, <c>Name</c> and <c>ConditionId</c> members and, for condition groups, the recursive
/// <c>Conditions</c> array.
/// </remarks>
public abstract class RuleConditionSchemaDefinitionBase : IRuleConditionSchemaDefinition
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string TypeDiscriminator { get; }

    /// <summary>
    /// Gets the human readable condition title. Returns <see langword="null"/> when no title is provided.
    /// </summary>
    protected virtual string DisplayText => null;

    /// <summary>
    /// Gets a description explaining what the condition evaluates.
    /// </summary>
    protected virtual string Description => null;

    /// <summary>
    /// Gets a value indicating whether the condition is a condition group that nests other conditions inside
    /// its own <c>Conditions</c> array.
    /// </summary>
    protected virtual bool IsGroup => false;

    /// <summary>
    /// Gets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    protected virtual IEnumerable<string> RequiredProperties => [];

    ValueTask<RuleConditionSchema> IRuleConditionSchemaDefinition.GetConditionSchemaAsync(
        RuleConditionSchemaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildConditionSchemaAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds the schema describing the condition. Override this method when the schema requires asynchronous work.
    /// </summary>
    /// <param name="context">The context describing shared schema fragments the condition can reuse.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected virtual ValueTask<RuleConditionSchema> BuildConditionSchemaAsync(
        RuleConditionSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildConditionSchemaCore(context));

    /// <summary>
    /// Builds the property definitions accepted by the condition, beyond the shared members.
    /// </summary>
    /// <param name="context">The context describing shared schema fragments the condition can reuse.</param>
    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context);

    /// <summary>
    /// Assembles the condition schema from the declared metadata and property definitions.
    /// </summary>
    /// <param name="context">The context describing shared schema fragments the condition can reuse.</param>
    protected virtual RuleConditionSchema BuildConditionSchemaCore(RuleConditionSchemaContext context)
    {
        var properties = GetPropertyDefinitions(context)?.ToArray() ?? [];

        return new RuleConditionSchema
        {
            DisplayText = DisplayText,
            Description = Description,
            IsGroup = IsGroup,
            Properties = properties,
            RequiredProperties = RequiredProperties?.ToArray() ?? [],
        };
    }
}
