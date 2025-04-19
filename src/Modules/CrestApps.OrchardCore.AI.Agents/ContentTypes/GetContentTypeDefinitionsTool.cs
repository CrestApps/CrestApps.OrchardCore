using System.Text.Json;
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
                    "contentType": {
                        "type": "string",
                        "description": "The content type to get the definitions for."
                    }
                },
                "additionalProperties": false,
                "required": ["contentType"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Retrieves the content type definition for a given content type.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("contentType", out var data))
        {
            return "Unable to find a contentType argument in the function arguments.";
        }

        string contentType;

        if (data is JsonElement jsonElement)
        {
            contentType = jsonElement.GetString();
        }
        else
        {
            contentType = data.ToString();
        }

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ViewContentTypes))
        {
            return "You do not have permission to view content types.";
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        if (definition is null)
        {
            return $"Unable to find a type definition that match the ContentType: {contentType}";
        }

        return JsonSerializer.Serialize(definition, JsonHelpers.ContentDefinitionSerializerOptions);
    }
}
