using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the users recipe step.
/// </summary>
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
                            ("Id", RecipeStepSchemaBuilders.Integer().Description("Legacy numeric user ID when importing from older exports.")),
                            ("UserId", RecipeStepSchemaBuilders.String().Description("Unique string user identifier.")),
                            ("UserName", RecipeStepSchemaBuilders.String().Description("Login user name.")),
                            ("Email", RecipeStepSchemaBuilders.String().Description("Primary email address.")),
                            ("PasswordHash", RecipeStepSchemaBuilders.String().Description("Existing password hash to import. Use only when migrating users.")),
                            ("EmailConfirmed", RecipeStepSchemaBuilders.Boolean().Description("Whether the email address is already confirmed.")),
                            ("IsEnabled", RecipeStepSchemaBuilders.Boolean().Description("Whether the user account is enabled.")),
                            ("NormalizedEmail", RecipeStepSchemaBuilders.String().Description("Normalized email value used by the identity store.")),
                            ("NormalizedUserName", RecipeStepSchemaBuilders.String().Description("Normalized user name used by the identity store.")),
                            ("SecurityStamp", RecipeStepSchemaBuilders.String().Description("ASP.NET Identity security stamp.")),
                            ("ResetToken", RecipeStepSchemaBuilders.String().Description("Optional password reset token to import.")),
                            ("PhoneNumber", RecipeStepSchemaBuilders.String().Description("User phone number.")),
                            ("PhoneNumberConfirmed", RecipeStepSchemaBuilders.Boolean().Description("Whether the phone number is already confirmed.")),
                            ("TwoFactorEnabled", RecipeStepSchemaBuilders.Boolean().Description("Whether two-factor authentication is enabled.")),
                            ("IsLockoutEnabled", RecipeStepSchemaBuilders.Boolean().Description("Whether the account can be locked out after failed sign-ins.")),
                            ("AccessFailedCount", RecipeStepSchemaBuilders.Integer().Description("Current failed sign-in count.")),
                            ("RoleNames", RecipeStepSchemaBuilders.StringArray().Description("Roles assigned to the user.")),
                        ]).Description("User record to create or update."),
                    1).Description("Users to import.")),
            ],
            ["Users"]);
}
