using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Agents.Recipes;

public sealed class ImportOrchardTool : ImportRecipeBaseTool
{
    public const string TheName = "importOrchardCoreRecipe";

    public ImportOrchardTool(IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers)
        : base(deploymentTargetHandlers)
    {
    }

    public override string Name => TheName;

    public override string Description => "Imports a dynamic OrchardCore JSON recipe to configure or modify the system.";
}
