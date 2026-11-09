using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Currencies" recipe step.
/// </summary>
public sealed class CurrenciesRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc />
    public override string Name => "Currencies";

    protected override JsonSchema CreateSchema()
    {
        var currencySchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("A currency, matched by ItemId, then by its code.")
            .Properties(
                ("ItemId", CatalogRecipeStepSchemas.ItemId("currency")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern("^[A-Z]{3}$").Description("The three-letter ISO-4217 currency code in upper case, for example USD. A currency without a matching ItemId is matched by this code.")),
                ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The friendly name shown in currency dropdowns, for example US Dollar.")))
            .Required("Name", "DisplayName")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Currencies").Description("Recipe step discriminator. Must be 'Currencies'.")),
                ("Currencies", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(currencySchema)
                    .MinItems(1)
                    .Description("The currencies to create or update.")))
            .Required("name", "Currencies")
            .AdditionalProperties(true)
            .Build();
    }
}
