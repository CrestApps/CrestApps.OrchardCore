using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the <c>move-attached-media-fields</c> recipe step.
/// </summary>
public sealed class MoveAttachedMediaFieldsRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc />
    public override string Name => "move-attached-media-fields";

    protected override JsonSchema CreateSchema()
    {
        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("ContentTypes", RecipeStepSchemaBuilders.Array(
                    RecipeStepSchemaBuilders.String())
                    .Description("Optional content type filter. Omit this property to process every content type that contains a Media Field configured with the Attached editor.")),
            ]);
    }
}
