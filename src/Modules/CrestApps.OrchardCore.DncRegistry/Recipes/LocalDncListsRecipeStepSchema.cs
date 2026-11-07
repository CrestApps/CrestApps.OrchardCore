using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.DncRegistry.Recipes;

/// <summary>
/// Describes the <c>LocalDncLists</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LocalDncListsRecipeStepSchema : RecipeStepSchemaBase
{
    public override string Name => LocalDncListsStep.StepKey;

    protected override JsonSchema CreateSchema()
    {
        var list = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("A local do-not-call list and its phone numbers.")
            .Properties(
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The name of the list. A list with the same name and country is skipped.")),
                ("CountryCode", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(2).MaxLength(2).Description("The ISO 3166-1 alpha-2 country code the numbers belong to, such as 'US'.")),
                ("PhoneNumbers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).MinItems(1).Description("The phone numbers, one per item.")))
            .Required("Name", "CountryCode", "PhoneNumbers")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Queues local do-not-call lists for import, as if their numbers were uploaded in a file. A list whose name and country already exist is skipped.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(LocalDncListsStep.StepKey).Description("Recipe step discriminator. Must be 'LocalDncLists'.")),
                (LocalDncListsStep.EntriesName, new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(list).Description("The lists to import.")))
            .Required("name", LocalDncListsStep.EntriesName)
            .AdditionalProperties(true)
            .Build();
    }
}
