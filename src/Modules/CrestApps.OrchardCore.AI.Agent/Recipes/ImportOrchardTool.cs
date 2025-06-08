using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public sealed class ImportOrchardTool : ImportRecipeBaseTool
{
    public const string TheName = "importOrchardCoreRecipe";

    public ImportOrchardTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        IOptions<DocumentJsonSerializerOptions> options)
        : base(deploymentTargetHandlers, options.Value)
    {
    }

    public override string Name => TheName;

    public override string Description => "Imports a dynamic OrchardCore JSON recipe to configure or modify the system.";
}
