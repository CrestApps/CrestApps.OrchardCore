using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu;

/// <summary>
/// Describes an admin menu node discovered from the registered
/// <see cref="IAdminNodeSchemaDefinition"/> contributions.
/// </summary>
public sealed class AdminNodeDescriptor
{
    /// <summary>
    /// Gets the admin menu node name, matching the node type name, for example <c>LinkAdminNode</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the polymorphic type discriminator serialized as the <c>$type</c> member, for example
    /// <c>OrchardCore.AdminMenu.AdminNodes.LinkAdminNode, OrchardCore.AdminMenu</c>.
    /// </summary>
    public required string TypeDiscriminator { get; init; }

    /// <summary>
    /// Gets the human readable node title shown in the admin menu editor.
    /// </summary>
    public string DisplayText { get; init; }

    /// <summary>
    /// Gets a description explaining what the node contributes to the admin menu.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the property definitions that are specific to the node, beyond the shared members.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; init; } = [];

    /// <summary>
    /// Gets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; init; } = [];
}
