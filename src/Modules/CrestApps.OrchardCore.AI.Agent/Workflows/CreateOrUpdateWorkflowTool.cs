using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

public sealed class CreateOrUpdateWorkflowTool : ImportRecipeBaseTool
{
    public const string TheName = "createOrUpdateWorkflow";

    public override string Name => TheName;

    public override string Description => "Creates or updates a workflow types.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        if (!await arguments.IsAuthorizedAsync(OrchardCorePermissions.ManageWorkflows))
        {
            return "You do not have permission to manage workflows.";
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return MissingArgument();
        }

        return await ProcessRecipeAsync(arguments.Services, recipe, cancellationToken);
    }
}
