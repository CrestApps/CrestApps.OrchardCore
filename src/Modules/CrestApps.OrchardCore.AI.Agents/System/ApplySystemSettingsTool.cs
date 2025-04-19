using CrestApps.OrchardCore.AI.Agents.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Agents.System;

public sealed class ApplySystemSettingsTool : ImportRecipeBaseTool
{
    public const string TheName = "applySiteSettings";

    public ApplySystemSettingsTool(IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers)
        : base(deploymentTargetHandlers)
    {
    }

    public override string Name => TheName;

    public override string Description => "Applies site settings or configurations to the system.";
}
