using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu.Nodes;

/// <summary>
/// Describes the recipe schema for the <c>ContentTypesAdminNode</c> admin menu node contributed by the
/// <c>OrchardCore.Contents</c> feature.
/// </summary>
public sealed class ContentTypesAdminNodeSchema : AdminNodeSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ContentTypesAdminNode";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Contents.AdminNodes.ContentTypesAdminNode, OrchardCore.Contents";

    /// <inheritdoc />
    protected override string DisplayText => "Content Types";

    /// <inheritdoc />
    protected override string Description => "Adds admin menu entries for content types, either all creatable types or a selected list.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(AdminNodeSchemaContext context)
    {
        yield return ("ShowAll", new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description("Whether every creatable content type is listed. When false, only the content types in the ContentTypes array are listed."));

        yield return ("IconClass", new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description("The CSS class of the icon shown next to each content type entry when no per-entry icon is set."));

        yield return ("ContentTypes", new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("ContentTypeName", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Description("The technical name of the content type to add.")),
                    ("ContentTypeDisplayName", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String | SchemaValueType.Null)
                        .Description("The display name of the content type shown in the menu.")),
                    ("IconClass", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String | SchemaValueType.Null)
                        .Description("The CSS class of the icon shown next to this content type entry.")))
                .Required("ContentTypeName")
                .AdditionalProperties(true))
            .Description("The explicit list of content types to add when ShowAll is false."));
    }
}
