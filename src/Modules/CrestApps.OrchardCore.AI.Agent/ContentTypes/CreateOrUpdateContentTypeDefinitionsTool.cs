using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class CreateOrUpdateContentTypeDefinitionsTool : ImportRecipeBaseTool
{
    public const string TheName = "applyContentTypeDefinitionFromRecipe";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CreateOrUpdateContentTypeDefinitionsTool(
        RecipeExecutionService recipeExecutionService,
        RecipeStepsService recipeStepsService,
        IEnumerable<IRecipeStep> recipeSteps,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(recipeExecutionService, recipeStepsService, recipeSteps)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override string Name => TheName;

    public override string Description => "Creates or updates a content type or part definition based on the provided JSON recipe.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.EditContentTypes))
        {
            return "You do not have permission to edit content types or parts.";
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return MissingArgument();
        }

        return await ProcessRecipeAsync(recipe);
    }
}
