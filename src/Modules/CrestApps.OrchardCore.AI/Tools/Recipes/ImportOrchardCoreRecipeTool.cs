using OrchardCore.Deployment.Services;

namespace CrestApps.OrchardCore.AI.Tools.Recipes;

public sealed class ImportOrchardCoreRecipeTool : ImportRecipeBaseTool
{
    public const string TheName = "importOrchardCoreRecipe";

    public ImportOrchardCoreRecipeTool(IDeploymentManager deploymentManager)
        : base(deploymentManager)
    {
    }

    public override string Name => TheName;

    public override string Description => "Imports a dynamic OrchardCore JSON recipe to configure or modify the system.";
}
