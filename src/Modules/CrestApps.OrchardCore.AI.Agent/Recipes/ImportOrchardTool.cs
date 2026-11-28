using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

/// <summary>
/// Represents the import orchard tool. It is the JSON import of the Deployment admin, so it needs the permission that
/// page checks: Import, which is security critical.
/// </summary>
public sealed class ImportOrchardTool : ImportRecipeBaseTool
{
    public const string TheName = "importOrchardCoreRecipe";

    public override string Name => TheName;

    public override string Description => $"Imports any Orchard Core JSON recipe. Before calling this tool, call '{GetRecipeJsonSchemaTool.TheName}' first whenever it is available, then build the recipe JSON to match that schema exactly.";

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        // Recipe import gate (review of PR 1334, F1 and F5): a recipe can change anything on the site, as the JSON import
        // of the Deployment admin can.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(arguments.Services, DeploymentPermissions.Import))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        return await base.InvokeCoreAsync(arguments, cancellationToken);
    }
}
