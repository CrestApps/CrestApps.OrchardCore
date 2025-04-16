using OrchardCore.Deployment.Services;

namespace CrestApps.OrchardCore.AI.Tools.Recipes;

public sealed class ApplySystemConfigurationsCoreRecipeTool : ImportRecipeBaseTool
{
    public const string TheName = "applySiteSettings";

    public ApplySystemConfigurationsCoreRecipeTool(IDeploymentManager deploymentManager)
        : base(deploymentManager)
    {
    }

    public override string Name => TheName;

    public override string Description => "Applies site settings or configurations to the system.";
}
