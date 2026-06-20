using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the custom user settings recipe step.
/// </summary>
public sealed class CustomUserSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "custom-user-settings";

    protected override JsonSchema CreateSchema()
    {
        var userSettingItem = RecipeStepSchemaBuilders.Object(
            [
                ("ContentType", RecipeStepSchemaBuilders.String().Description("Custom user settings content type to create or update for the user.")),
            ],
            ["ContentType"]).Description("Single custom user settings content item payload.");

        var userSettingsGroup = RecipeStepSchemaBuilders.Array(
            RecipeStepSchemaBuilders.Object(
                [
                    ("userId", RecipeStepSchemaBuilders.String().Description("Target user identifier.")),
                    ("user-custom-user-settings", RecipeStepSchemaBuilders.Array(userSettingItem, 1).Description("Custom user settings content items assigned to the user.")),
                ],
                ["userId", "user-custom-user-settings"]).Description("Custom settings group for one user."),
            1).Description("Groups of users and their custom settings items.");

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", RecipeStepSchemaBuilders.String().Const(Name).Description($"Recipe step discriminator. Must be '{Name}'.")))
            .Required("name")
            .MinProperties(2)
            .AdditionalProperties(userSettingsGroup)
            .Description("Each additional property can use any collection name and must contain an array of user custom settings entries.")
            .Build();
    }
}
