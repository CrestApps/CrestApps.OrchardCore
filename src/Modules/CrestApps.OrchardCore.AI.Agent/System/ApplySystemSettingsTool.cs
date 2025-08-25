using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Agent.Schemas;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agent.System;

public sealed class ApplySystemSettingsTool : ImportRecipeBaseTool
{
    public const string TheName = "applySiteSettings";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ApplySystemSettingsTool(
        RecipeExecutionService recipeExecutionService,
        RecipeStepsService recipeStepsService,
        IOptions<DocumentJsonSerializerOptions> options,
        IEnumerable<IRecipeStep> recipeSteps,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(recipeExecutionService, recipeStepsService, recipeSteps)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override string Name => TheName;

    public override string Description => "Applies site settings or configurations to the system.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, DeploymentPermissions.Import))
        {
            return "You do not have permission to import recipes.";
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return MissingArgument();
        }

        return await ProcessRecipeAsync(recipe);
    }
}
