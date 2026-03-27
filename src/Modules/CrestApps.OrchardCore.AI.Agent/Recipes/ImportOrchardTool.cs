using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Services;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public sealed class ImportOrchardTool : ImportRecipeBaseTool
{
    public const string TheName = "importOrchardCoreRecipe";

    public ImportOrchardTool(
        RecipeExecutionService recipeExecutionService,
        RecipeStepsService recipeStepsService,
        IEnumerable<IRecipeStep> recipeSteps)
        : base(recipeExecutionService, recipeStepsService, recipeSteps)
    {
    }

    public override string Name => TheName;

    public override string Description => "Imports any Orchard Core JSON recipe.";
}
