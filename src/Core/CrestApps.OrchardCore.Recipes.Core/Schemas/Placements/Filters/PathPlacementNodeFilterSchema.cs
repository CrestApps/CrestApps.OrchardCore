using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Placements.Filters;

/// <summary>
/// Describes the recipe schema for the <c>path</c> placement node filter.
/// </summary>
public sealed class PathPlacementNodeFilterSchema : PlacementNodeFilterSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Key { get; } = "path";

    /// <inheritdoc />
    protected override string DisplayText => "Path";

    /// <inheritdoc />
    protected override string Description => "Applies the placement only when the current request path matches. A trailing '*' matches by prefix. Accepts a single path or an array of paths.";

    /// <inheritdoc />
    protected override JsonSchemaBuilder GetValueSchema(PlacementNodeFilterSchemaContext context)
        => new JsonSchemaBuilder()
            .OneOf(
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("A single request path to match, such as /about or ~/blog/*."),
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("A set of request paths, any of which matches."))
            .Description("The request path or paths that activate the placement.");
}
