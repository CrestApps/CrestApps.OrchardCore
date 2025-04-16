using CrestApps.OrchardCore.AI.Tools.Recipes;
using OrchardCore.Deployment.Services;

namespace CrestApps.OrchardCore.AI.Tools.ContentTypes;

public sealed class CreateOrUpdateContentTypeDefinitionsOrchardCoreTool : ImportRecipeBaseTool
{
    public const string TheName = "applyContentTypeDefinitionFromRecipe";

    public CreateOrUpdateContentTypeDefinitionsOrchardCoreTool(
        IDeploymentManager deploymentManager)
        : base(deploymentManager)
    {
    }

    public override string Name => TheName;

    public override string Description => "Creates or updates a content type definition based on the configuration provided in a recipe.";
}
