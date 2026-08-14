using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu;

/// <summary>
/// Describes the recipe payload of a single admin menu node inside a menu's <c>MenuItems</c> array.
/// </summary>
public sealed class AdminNodeSchema
{
    /// <summary>
    /// Gets or sets the human readable node title shown in the admin menu editor.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the node contributes to the admin menu.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the property definitions that are specific to the node, beyond the shared members the
    /// schema service adds, such as <c>$type</c>, <c>UniqueId</c>, <c>Enabled</c> and the recursive <c>Items</c> array.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; set; } = [];
}
