using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu.Nodes;

/// <summary>
/// Describes the recipe schema for the <c>LinkAdminNode</c> admin menu node.
/// </summary>
public sealed class LinkAdminNodeSchema : AdminNodeSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "LinkAdminNode";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.AdminMenu.AdminNodes.LinkAdminNode, OrchardCore.AdminMenu";

    /// <inheritdoc />
    protected override string DisplayText => "Link";

    /// <inheritdoc />
    protected override string Description => "Adds a single link to the admin menu.";

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["LinkText", "LinkUrl"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(AdminNodeSchemaContext context)
    {
        yield return ("LinkText", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The text displayed for the link."));

        yield return ("LinkUrl", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The URL the link points to. It can be an absolute URL or a relative admin URL, and it supports script expressions such as [js: ...]."));

        yield return ("IconClass", new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description("The CSS class of the icon shown next to the link, for example fas fa-rss."));

        yield return ("PermissionNames", new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description("The names of the permissions the user must have for the link to be shown."));
    }
}
