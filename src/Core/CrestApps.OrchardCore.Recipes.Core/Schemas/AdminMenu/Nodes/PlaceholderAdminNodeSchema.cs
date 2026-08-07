using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu.Nodes;

/// <summary>
/// Describes the recipe schema for the <c>PlaceholderAdminNode</c> admin menu node.
/// </summary>
public sealed class PlaceholderAdminNodeSchema : AdminNodeSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "PlaceholderAdminNode";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.AdminMenu.AdminNodes.PlaceholderAdminNode, OrchardCore.AdminMenu";

    /// <inheritdoc />
    protected override string DisplayText => "Placeholder";

    /// <inheritdoc />
    protected override string Description => "Adds a non-clickable label that groups child nodes in the admin menu.";

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["LinkText"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(AdminNodeSchemaContext context)
    {
        yield return ("LinkText", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The text displayed for the placeholder group."));

        yield return ("IconClass", new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description("The CSS class of the icon shown next to the placeholder, for example fas fa-sitemap."));

        yield return ("PermissionNames", new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description("The names of the permissions the user must have for the placeholder to be shown."));
    }
}
