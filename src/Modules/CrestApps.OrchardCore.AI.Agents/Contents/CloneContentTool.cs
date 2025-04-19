using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agents.Contents;

public sealed class CloneContentTool : AIFunction
{
    public const string TheName = "cloneContentItem";

    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CloneContentTool(
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;

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

    public override string Description => "Clones the data from one content item into another existing content item.";

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

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.CloneContent))
        {
            return "You do not have permission to clone content items.";
        }

        var clone = await _contentManager.CloneAsync(contentItem);

        return "Content item was successfully cloned. The ContentItemId of the new contentItem is: " + clone.ContentItemId;
    }
}
