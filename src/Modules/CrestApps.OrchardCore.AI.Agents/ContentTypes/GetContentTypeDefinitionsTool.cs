using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Agents.ContentTypes;

public sealed class GetContentTypeDefinitionsTool : AIFunction
{
    public const string TheName = "getContentTypeDefinition";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public GetContentTypeDefinitionsTool(
        IContentDefinitionManager contentDefinitionManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "The name of the content type to get the definitions for."
                    }
                },
                "additionalProperties": false,
                "required": ["name"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Retrieves the content type definition for a given content type.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "Unable to find a contentType argument in the function arguments.";
        }

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ViewContentTypes))
        {
            return "You do not have permission to view content types.";
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(name);

        if (definition is null)
        {
            return $"Unable to find a type definition that match the ContentType: {name}";
        }

        return JsonSerializer.Serialize(definition, JsonHelpers.ContentDefinitionSerializerOptions);
    }
}
