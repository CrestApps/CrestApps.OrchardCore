using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Placements.Filters;

/// <summary>
/// Describes the recipe schema for the <c>contentPart</c> placement node filter.
/// </summary>
public sealed class ContentPartPlacementNodeFilterSchema : PlacementNodeFilterSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Key { get; } = "contentPart";

    /// <inheritdoc />
    protected override string DisplayText => "Content Part";

    /// <inheritdoc />
    protected override string Description => "Applies the placement only when the content item has a matching content part. Accepts a single content part or an array of content parts.";

    /// <inheritdoc />
    protected override JsonSchemaBuilder GetValueSchema(PlacementNodeFilterSchemaContext context)
        => new JsonSchemaBuilder()
            .OneOf(
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("A single content part name to match, such as BodyPart."),
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("A set of content part names, any of which matches."))
            .Description("The content part or parts that activate the placement.");
}
