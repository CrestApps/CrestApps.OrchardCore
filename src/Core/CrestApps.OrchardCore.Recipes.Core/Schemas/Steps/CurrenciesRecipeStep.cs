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
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ISO-4217 currency code, for example USD.")),
                ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The friendly name shown in currency dropdowns, for example US Dollar.")),
                ("OwnerId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The user id of the user creating the entry. Leave it blank to use the current user's Id.")),
                ("Author", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The username of the user creating the entry. Leave it blank to use the current username.")))
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
