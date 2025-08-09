using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.System;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using Json.Schema;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public sealed class ImportOrchardTool : ImportRecipeBaseTool
{
    public const string TheName = "importOrchardCoreRecipe";

    public ImportOrchardTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        IEnumerable<IRecipeStepHandler> handlers,
        IOptions<DocumentJsonSerializerOptions> options)
        : base(deploymentTargetHandlers, handlers, options.Value)
    {
    }

    public override string Name => TheName;

    public override string Description =>
        $"""
        Imports any Orchard Core JSON recipe.

        Note: This tool should NOT be used for the following scenarios:
        - Applying site settings: use the '{ApplySystemSettingsTool.TheName}' tool instead.
        - Creating or updating workflow: use the '{CreateOrUpdateWorkflowTool.TheName}' tool instead.
        - Creating or updating content types or parts: use the '{CreateOrUpdateContentTypeDefinitionsTool.TheName}' tool instead.
        """;

    protected override ValueTask<bool> BuildingRecipeSchemaAsync(JsonSchemaBuilder builder)
    {
        // Do nothing here as the schema is already defined in the base class.

        return ValueTask.FromResult(false);
    }
}
