using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Represents the create or update content type definitions tool. The recipe is imported as the JSON import of the
/// Deployment admin imports one and can hold any step, so the caller needs that page's permission (Import) as well as
/// the right to edit content types.
/// </summary>
public sealed class CreateOrUpdateContentTypeDefinitionsTool : ImportRecipeBaseTool
{
    public const string TheName = "applyContentTypeDefinitionFromRecipe";

    public override string Name => TheName;

    public override string Description => "Creates or updates a content type or part definition based on the provided JSON recipe.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        // Recipe import gate (review of PR 1334, F1 and F5): the same check as importOrchardCoreRecipe, applySiteSettings
        // and createOrUpdateWorkflow, because the recipe can hold steps other than content definitions.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(arguments.Services, OrchardCorePermissions.EditContentTypes) ||
            !await AgentToolAuthorization.IsAuthorizedAsync(arguments.Services, DeploymentPermissions.Import))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var logger = arguments.Services.GetRequiredService<ILogger<CreateOrUpdateContentTypeDefinitionsTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'recipe' argument.", TheName);

            return MissingArgument();
        }

        var result = await ProcessRecipeAsync(arguments.Services, recipe, logger, cancellationToken);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
        }

        return result;
    }
}
