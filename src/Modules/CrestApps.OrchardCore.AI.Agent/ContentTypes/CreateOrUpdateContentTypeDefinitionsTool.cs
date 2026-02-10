using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class CreateOrUpdateContentTypeDefinitionsTool : ImportRecipeBaseTool
{
    public const string TheName = "applyContentTypeDefinitionFromRecipe";

    public override string Name => TheName;

    public override string Description => "Creates or updates a content type or part definition based on the provided JSON recipe.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        if (!await arguments.IsAuthorizedAsync(OrchardCorePermissions.EditContentTypes))
        {
            return "You do not have permission to edit content types or parts.";
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return MissingArgument();
        }

        return await ProcessRecipeAsync(arguments.Services, recipe, cancellationToken);
    }
}
