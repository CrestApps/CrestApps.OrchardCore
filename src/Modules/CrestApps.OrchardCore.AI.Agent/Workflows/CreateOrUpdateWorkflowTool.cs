using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;
using OrchardCore.Workflows;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Represents the create or update workflow tool.
/// </summary>
public sealed class CreateOrUpdateWorkflowTool : ImportRecipeBaseTool
{
    public const string TheName = "createOrUpdateWorkflow";

    public override string Name => TheName;

    public override string Description => "Creates or updates a workflow types.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        // Recipe import gate (review of PR 1334, F1 and F5): the recipe is imported as the JSON import of the Deployment
        // admin imports one, and it can hold steps other than workflows, so the caller needs that page's permission as
        // well as the right to manage workflows.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(arguments.Services, WorkflowsPermissions.ManageWorkflows) ||
            !await AgentToolAuthorization.IsAuthorizedAsync(arguments.Services, DeploymentPermissions.Import))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var logger = arguments.Services.GetRequiredService<ILogger<CreateOrUpdateWorkflowTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument '{ArgumentName}'.", Name, "recipe");

            return MissingArgument();
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return await ProcessRecipeAsync(arguments.Services, recipe, logger, cancellationToken);
    }
}
