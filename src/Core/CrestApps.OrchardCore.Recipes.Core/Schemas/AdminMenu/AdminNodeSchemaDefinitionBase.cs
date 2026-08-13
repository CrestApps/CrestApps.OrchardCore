using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu;

/// <summary>
/// Provides the standard implementation surface for admin menu node schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe an admin menu node in the <c>AdminMenu</c> recipe step. Implementations
/// only supply the node name, its type discriminator and the members it accepts; the schema service assembles
/// the shared members, such as <c>$type</c>, <c>UniqueId</c> and <c>Enabled</c>, and the recursive
/// <c>Items</c> array.
/// </remarks>
public abstract class AdminNodeSchemaDefinitionBase : IAdminNodeSchemaDefinition
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string TypeDiscriminator { get; }

    /// <summary>
    /// Gets the human readable node title. Returns <see langword="null"/> when no title is provided.
    /// </summary>
    protected virtual string DisplayText => null;

    /// <summary>
    /// Gets a description explaining what the node contributes to the admin menu.
    /// </summary>
    protected virtual string Description => null;

    /// <summary>
    /// Gets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    protected virtual IEnumerable<string> RequiredProperties => [];

    ValueTask<AdminNodeSchema> IAdminNodeSchemaDefinition.GetNodeSchemaAsync(
        AdminNodeSchemaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildNodeSchemaAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds the schema describing the node. Override this method when the schema requires asynchronous work.
    /// </summary>
    /// <param name="context">The context describing the node being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected virtual ValueTask<AdminNodeSchema> BuildNodeSchemaAsync(
        AdminNodeSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildNodeSchemaCore(context));

    /// <summary>
    /// Builds the property definitions accepted by the node, beyond the shared members.
    /// </summary>
    /// <param name="context">The context describing the node being documented.</param>
    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(AdminNodeSchemaContext context);

    /// <summary>
    /// Assembles the node schema from the declared metadata and property definitions.
    /// </summary>
    /// <param name="context">The context describing the node being documented.</param>
    protected virtual AdminNodeSchema BuildNodeSchemaCore(AdminNodeSchemaContext context)
    {
        var properties = GetPropertyDefinitions(context)?.ToArray() ?? [];

        return new AdminNodeSchema
        {
            DisplayText = DisplayText,
            Description = Description,
            Properties = properties,
            RequiredProperties = RequiredProperties?.ToArray() ?? [],
        };
    }
}
