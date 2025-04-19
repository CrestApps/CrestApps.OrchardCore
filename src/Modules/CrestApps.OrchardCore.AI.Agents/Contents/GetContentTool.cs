using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agents.Contents;

public sealed class GetContentTool : AIFunction
{
    public const string TheName = "getContentItemById";

    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly DocumentJsonSerializerOptions _options;

    public GetContentTool(
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptions<DocumentJsonSerializerOptions> options)
    {
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _options = options.Value;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "type": "object",
                "properties": {
                    "contentItemId": {
                        "type": "string",
                        "description": "The string representation of the content item's ContentItemId."
                    }
                },
                "additionalProperties": false,
                "required": ["contentItemId"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Retrieves a content item using its unique content item ID.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("contentItemId", out var data))
        {
            return "Unable to find a contentItemId argument in the function arguments.";
        }

        string contentItemId;

        if (data is JsonElement jsonElement)
        {
            contentItemId = jsonElement.GetString();
        }
        else
        {
            contentItemId = data.ToString();
        }

        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem is null)
        {
            return $"Unable to find a content item that match the ContentItemId: {contentItemId}";
        }

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.ViewContent))
        {
            return "You do not have permission to view content items.";
        }

        return JsonSerializer.Serialize(contentItem, _options.SerializerOptions);
    }
}
