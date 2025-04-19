using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Agents.ContentTypes;

public sealed class GetContentPartDefinitionsTool : AIFunction
{
    public const string TheName = "getContentPartDefinition";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public GetContentPartDefinitionsTool(
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
                    "contentPart": {
                        "type": "string",
                        "description": "The content part to get the definitions for."
                    }
                },
                "additionalProperties": false,
                "required": ["contentPart"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Retrieves the content part definition for a given content part.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("contentPart", out var data))
        {
            return "Unable to find a contentPart argument in the function arguments.";
        }

        string contentPart;

        if (data is JsonElement jsonElement)
        {
            contentPart = jsonElement.GetString();
        }
        else
        {
            contentPart = data.ToString();
        }

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ViewContentTypes))
        {
            return "You do not have permission to view content types.";
        }

        var definition = await _contentDefinitionManager.GetPartDefinitionAsync(contentPart);

        if (definition is null)
        {
            return $"Unable to find a part definition that match the ContentPart: {contentPart}";
        }

        return JsonSerializer.Serialize(definition, JsonHelpers.ContentDefinitionSerializerOptions);
    }
}
