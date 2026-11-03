using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Accounts.B2B.Recipes;

/// <summary>
/// Describes the PriceLists recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PriceListsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PriceListsStep.Name;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PriceListsStep.Name).Description("Recipe step discriminator. Must be 'PriceLists'.")),
                ("PriceLists", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The price list's id. A list with this id is updated; otherwise a list with the same name is, or a new one is made with this id.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The price list's name, as shown in an account's B2B settings.")),
                            ("PercentOff", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Minimum(0).Maximum(100).Description("A discount off the store's price for every SKU without its own price, from 0 to 100.")),
                            ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The currency the list's prices are in, such as 'USD'. Its prices apply only to buyers shopping in it.")),
                            ("Entries", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Sku", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The product variant's SKU.")),
                                        ("UnitPrice", new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Description("The price of one unit, in the list's currency.")))
                                    .Required("Sku", "UnitPrice"))
                                .Description("The prices by SKU.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The price lists to create or update.")))
            .Required("name", "PriceLists")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
