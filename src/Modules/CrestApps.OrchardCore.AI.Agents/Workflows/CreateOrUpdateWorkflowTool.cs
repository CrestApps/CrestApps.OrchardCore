using CrestApps.OrchardCore.AI.Agents.Recipes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agents.Workflows;

public sealed class CreateOrUpdateWorkflowTool : ImportRecipeBaseTool
{
    public const string TheName = "CreateWorkflowFromRecipe";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CreateOrUpdateWorkflowTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        IOptions<DocumentJsonSerializerOptions> options,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(deploymentTargetHandlers, options.Value)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override string Name => TheName;

    public override string Description => "Creates or updates a workflow.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageWorkflows))
        {
            return "You do not have permission to manage workflows.";
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return MissingArgument();
        }

        return await ProcessRecipeAsync(recipe, cancellationToken);
    }
}
