using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the microsoft account settings recipe step.
/// </summary>
public sealed class MicrosoftAccountSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "MicrosoftAccountSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("AppId", RecipeStepSchemaBuilders.String().Description("Microsoft application (client) ID.")),
                ("AppSecret", RecipeStepSchemaBuilders.String().Description("Microsoft application client secret.")),
                ("CallbackPath", RecipeStepSchemaBuilders.String().Description("Relative callback path that Microsoft redirects back to after sign-in.")),
            ]);
}
