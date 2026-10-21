using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Stock" recipe step — sets the counted on-hand quantity of products and variants. A row is matched by
/// <c>ContentItemId</c> and <c>VariantId</c>, else by <c>Sku</c>; a row that matches nothing is a recipe error.
/// </summary>
public sealed class StockRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "Stock";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
    {
        var item = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("A stock count. It names the product by 'Sku', or by 'ContentItemId' and, for a variant, 'VariantId'.")
            .Properties(
                ("Sku", NullableString("The SKU of the product or variant. Used when the ids match nothing, so it matches on any site.")),
                ("ContentItemId", NullableString("The content item id of the product. A product imported by an earlier step of the same recipe is synced before it is counted.")),
                ("VariantId", NullableString("The id of the product's variant, when the count is for a variant.")),
                ("OnHand", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("The counted quantity on hand, a whole number of zero or more.")))
            .Required("OnHand")
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Items", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(item)
                    .Description("The stock counts to apply.")),
            ],
            ["Items"]);
    }

    private static JsonSchemaBuilder NullableString(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
