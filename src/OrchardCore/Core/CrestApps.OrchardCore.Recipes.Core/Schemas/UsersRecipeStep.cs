using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class UsersRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "Users";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Users", RecipeStepSchemaBuilders.Array(
                    RecipeStepSchemaBuilders.Object(
                        [
                            ("Id", RecipeStepSchemaBuilders.Integer()),
                            ("UserId", RecipeStepSchemaBuilders.String()),
                            ("UserName", RecipeStepSchemaBuilders.String()),
                            ("Email", RecipeStepSchemaBuilders.String()),
                            ("PasswordHash", RecipeStepSchemaBuilders.String()),
                            ("EmailConfirmed", RecipeStepSchemaBuilders.Boolean()),
                            ("IsEnabled", RecipeStepSchemaBuilders.Boolean()),
                            ("NormalizedEmail", RecipeStepSchemaBuilders.String()),
                            ("NormalizedUserName", RecipeStepSchemaBuilders.String()),
                            ("SecurityStamp", RecipeStepSchemaBuilders.String()),
                            ("ResetToken", RecipeStepSchemaBuilders.String()),
                            ("PhoneNumber", RecipeStepSchemaBuilders.String()),
                            ("PhoneNumberConfirmed", RecipeStepSchemaBuilders.Boolean()),
                            ("TwoFactorEnabled", RecipeStepSchemaBuilders.Boolean()),
                            ("IsLockoutEnabled", RecipeStepSchemaBuilders.Boolean()),
                            ("AccessFailedCount", RecipeStepSchemaBuilders.Integer()),
                            ("RoleNames", RecipeStepSchemaBuilders.StringArray()),
                        ]),
                    1)),
            ],
            ["Users"]);
}
