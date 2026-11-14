using OrchardCore.Security.Permissions;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Represents the create or update workflow tool.
/// </summary>
public sealed class CreateOrUpdateWorkflowTool : ImportRecipeBaseTool
{
    public const string TheName = "createOrUpdateWorkflow";

    public override string Name => TheName;

    // A recipe runs every step it carries, so the narrower permission is held to the workflow step: without the list,
    // ManageWorkflows would import a Roles, Users or settings step too.
    private static readonly string[] _allowedSteps = ["WorkflowType"];

    protected override Permission RequiredPermission => OrchardCorePermissions.ManageWorkflows;

    public override string Description => "Creates or updates a workflow types.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        if (!await IsCallerAuthorizedAsync(arguments.Services))
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

        return await ProcessRecipeAsync(arguments.Services, recipe, logger, _allowedSteps, cancellationToken);
    }
}
