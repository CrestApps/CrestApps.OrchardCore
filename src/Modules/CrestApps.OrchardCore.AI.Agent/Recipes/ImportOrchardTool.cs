using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

/// <summary>
/// Represents the import orchard tool.
/// </summary>
public sealed class ImportOrchardTool : ImportRecipeBaseTool
{
    public const string TheName = "importOrchardCoreRecipe";

    public override string Name => TheName;

    public override string Description => $"Imports any Orchard Core JSON recipe. Before calling this tool, call '{GetRecipeJsonSchemaTool.TheName}' first whenever it is available, then build the recipe JSON to match that schema exactly.";
}
