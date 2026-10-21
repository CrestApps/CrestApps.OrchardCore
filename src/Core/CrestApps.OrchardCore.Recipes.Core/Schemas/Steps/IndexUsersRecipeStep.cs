using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "IndexUsers" recipe step — saves every user again, in batches, so the user indexes are rebuilt.
/// </summary>
public sealed class IndexUsersRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "IndexUsers";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("IncludeDisabledUsers", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(false).Description("Whether disabled users are indexed too. By default only enabled users are.")),
                ("BatchSize", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Maximum(1000).Default(250).Description("How many users are saved per batch. Defaults to 250; values over 1000 are capped at 1000.")),
            ]);
}
