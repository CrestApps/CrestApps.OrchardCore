using CrestApps.OrchardCore.AI.Tools.Recipes;
using OrchardCore.Deployment.Services;

namespace CrestApps.OrchardCore.AI.Tools.ContentTypes;

public sealed class CreateOrUpdateContentPartDefinitionsOrchardCoreTool : ImportRecipeBaseTool
{
    public const string TheName = "applyContentPartDefinitionFromRecipe";

    public CreateOrUpdateContentPartDefinitionsOrchardCoreTool(
        IDeploymentManager deploymentManager)
        : base(deploymentManager)
    {
    }

    public override string Name => TheName;

    public override string Description => "Creates or updates a content part definition using the configuration provided in a recipe.";
}
