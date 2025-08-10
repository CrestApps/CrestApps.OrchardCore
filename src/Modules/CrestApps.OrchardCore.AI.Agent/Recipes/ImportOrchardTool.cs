using CrestApps.OrchardCore.AI.Agent.Schemas;
using CrestApps.OrchardCore.AI.Agent.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public sealed class ImportOrchardTool : ImportRecipeBaseTool
{
    public const string TheName = "importOrchardCoreRecipe";

    public ImportOrchardTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        RecipeStepsService recipeStepsService,
        IEnumerable<IRecipeStep> recipeSteps,
        IOptions<DocumentJsonSerializerOptions> options)
        : base(deploymentTargetHandlers, recipeStepsService, recipeSteps, options.Value)
    {
    }

    public override string Name => TheName;

    public override string Description => "Imports any Orchard Core JSON recipe.";
}
