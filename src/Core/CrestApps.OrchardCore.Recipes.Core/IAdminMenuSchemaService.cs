using CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Builds JSON schemas describing the admin menu nodes available on the current tenant, used by the
/// <c>AdminMenu</c> recipe step to describe each menu and its recursive <c>MenuItems</c> array.
/// </summary>
public interface IAdminMenuSchemaService
{
    /// <summary>
    /// Gets a descriptor for every admin menu node contributed through an
    /// <see cref="IAdminNodeSchemaDefinition"/>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<AdminNodeDescriptor>> GetNodeDescriptorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the recursive schema describing a single entry of a menu's <c>MenuItems</c> array. Nodes nest
    /// additional nodes of the same shape through their <c>Items</c> array.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetNodeSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema describing a single admin menu object, as used by an entry of the <c>AdminMenu</c>
    /// step's <c>data</c> array.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetAdminMenuSchemaAsync(CancellationToken cancellationToken = default);
}
