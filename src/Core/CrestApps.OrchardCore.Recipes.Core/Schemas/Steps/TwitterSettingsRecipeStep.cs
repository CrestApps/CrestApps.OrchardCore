using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the twitter settings recipe step.
/// </summary>
public sealed class TwitterSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "TwitterSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("ConsumerKey", RecipeStepSchemaBuilders.String().Description("Twitter/X API consumer key.")),
                ("ConsumerSecret", RecipeStepSchemaBuilders.String().Description("Twitter/X API consumer secret.")),
                ("AccessToken", RecipeStepSchemaBuilders.String().Description("Twitter/X API access token used for authenticated requests.")),
                ("AccessTokenSecret", RecipeStepSchemaBuilders.String().Description("Twitter/X API access token secret.")),
            ]);
}
