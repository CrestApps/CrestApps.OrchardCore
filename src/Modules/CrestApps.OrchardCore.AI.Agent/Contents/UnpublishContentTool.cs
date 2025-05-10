using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

public sealed class UnpublishContentTool : AIFunction
{
    public const string TheName = "unpublishContentItem";

    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public UnpublishContentTool(
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
                  "description": "The unique identifier of the content item, represented as a string (ContentItemId)."
                }
              },
              "required": ["contentItemId"],
              "additionalProperties": false
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Changes the status of a published content item to draft, making it editable without being publicly visible.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.PublishContent))
        {
            return "You do not have permission to publish content items.";
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "Unable to find a contentItemId argument in the function arguments.";
        }

        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem is null)
        {
            return $"Unable to find a content item that match the ContentItemId: {contentItemId}";
        }

        await _contentManager.UnpublishAsync(contentItem);

        return "Content item was successfully unpublished";
    }
}
