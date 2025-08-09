using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agent.System;

public sealed class ApplySystemSettingsTool : ImportRecipeBaseTool
{
    public const string TheName = "applySiteSettings";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ApplySystemSettingsTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        IEnumerable<IRecipeStepHandler> handlers,
        IOptions<DocumentJsonSerializerOptions> options,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(deploymentTargetHandlers, handlers, options.Value)
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

        return await ProcessRecipeAsync(recipe, cancellationToken);
    }

    protected override ValueTask<bool> BuildingRecipeSchemaAsync(JsonSchemaBuilder builder)
    {
        builder
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("settings")
                )
            )
            .Required("name")
            .MinProperties(2)     // at least "name" plus one other key
            .AdditionalProperties(true); // allow any other keys of any type

        return ValueTask.FromResult(true);
    }
}
