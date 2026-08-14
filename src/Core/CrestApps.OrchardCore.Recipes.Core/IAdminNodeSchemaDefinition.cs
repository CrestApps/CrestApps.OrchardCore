using CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces the JSON schema and metadata that describe a single admin menu node inside the
/// <c>MenuItems</c> array of the <c>AdminMenu</c> recipe step.
/// </summary>
/// <remarks>
/// Implement this interface when a module contributes a custom admin menu node and wants the generated recipe
/// schema to describe the node's members. Registering the implementation as
/// <see cref="IAdminNodeSchemaDefinition"/> is enough for the <c>AdminMenu</c> recipe step to pick it up.
/// Prefer deriving from
/// <see cref="CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu.AdminNodeSchemaDefinitionBase"/>, which
/// handles the standard node envelope, including the shared members and the recursive <c>Items</c> array.
/// </remarks>
public interface IAdminNodeSchemaDefinition
{
    /// <summary>
    /// Gets the admin menu node name that this definition describes, matching the node type name, for example
    /// <c>LinkAdminNode</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the polymorphic type discriminator serialized as the <c>$type</c> member, for example
    /// <c>OrchardCore.AdminMenu.AdminNodes.LinkAdminNode, OrchardCore.AdminMenu</c>.
    /// </summary>
    string TypeDiscriminator { get; }

    /// <summary>
    /// Builds the schema and metadata describing the admin menu node.
    /// </summary>
    /// <param name="context">The context describing the node being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<AdminNodeSchema> GetNodeSchemaAsync(AdminNodeSchemaContext context, CancellationToken cancellationToken = default);
}
