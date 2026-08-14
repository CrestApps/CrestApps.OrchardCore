using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Placements.Filters;

/// <summary>
/// Describes the recipe schema for the <c>contentType</c> placement node filter.
/// </summary>
public sealed class ContentTypePlacementNodeFilterSchema : PlacementNodeFilterSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Key { get; } = "contentType";

    /// <inheritdoc />
    protected override string DisplayText => "Content Type";

    /// <inheritdoc />
    protected override string Description => "Applies the placement only when the shape belongs to a matching content type or stereotype. A trailing '*' matches by prefix. Accepts a single content type or an array of content types.";

    /// <inheritdoc />
    protected override JsonSchemaBuilder GetValueSchema(PlacementNodeFilterSchemaContext context)
        => new JsonSchemaBuilder()
            .OneOf(
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("A single content type or stereotype to match, such as Article or Widget*."),
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("A set of content types or stereotypes, any of which matches."))
            .Description("The content type or types that activate the placement.");
}
