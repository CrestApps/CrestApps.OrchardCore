using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu.Nodes;

/// <summary>
/// Describes the recipe schema for the <c>ListsAdminNode</c> admin menu node contributed by the
/// <c>OrchardCore.Lists</c> feature.
/// </summary>
public sealed class ListsAdminNodeSchema : AdminNodeSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ListsAdminNode";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Lists.AdminNodes.ListsAdminNode, OrchardCore.Lists";

    /// <inheritdoc />
    protected override string DisplayText => "Lists";

    /// <inheritdoc />
    protected override string Description => "Adds admin menu entries for the content items of a list content type.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(AdminNodeSchemaContext context)
    {
        yield return ("ContentType", new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description("The technical name of the list content type whose items are added to the menu."));

        yield return ("AddContentTypeAsParent", new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description("Whether a parent node is added for the content type, with the list items nested under it. Defaults to true."));

        yield return ("IconForParentLink", new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description("The CSS class of the icon shown next to the parent node."));

        yield return ("IconForContentItems", new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description("The CSS class of the icon shown next to each list item node."));
    }
}
