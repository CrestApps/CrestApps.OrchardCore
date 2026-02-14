using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Agent.System;

public sealed class ApplySystemSettingsTool : ImportRecipeBaseTool
{
    public const string TheName = "applySiteSettings";

    public override string Name => TheName;

    public override string Description => "Applies site settings or configurations to the system.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        if (!await arguments.IsAuthorizedAsync(DeploymentPermissions.Import))
        {
            return "You do not have permission to import recipes.";
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return MissingArgument();
        }

        return await ProcessRecipeAsync(arguments.Services, recipe, cancellationToken);
    }
}
