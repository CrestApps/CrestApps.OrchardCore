using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class CustomUserSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "custom-user-settings";

    protected override JsonSchema CreateSchema()
    {
        var userSettingItem = RecipeStepSchemaBuilders.Object(
            [
                ("ContentType", RecipeStepSchemaBuilders.String()),
            ],
            ["ContentType"]);

        var userSettingsGroup = RecipeStepSchemaBuilders.Array(
            RecipeStepSchemaBuilders.Object(
                [
                    ("userId", RecipeStepSchemaBuilders.String()),
                    ("user-custom-user-settings", RecipeStepSchemaBuilders.Array(userSettingItem, 1)),
                ],
                ["userId", "user-custom-user-settings"]),
            1);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", RecipeStepSchemaBuilders.String().Const(Name)))
            .Required("name")
            .MinProperties(2)
            .AdditionalProperties(userSettingsGroup)
            .Description("Each additional property can use any collection name and must contain an array of user custom settings entries.")
            .Build();
    }
}
