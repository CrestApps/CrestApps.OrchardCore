using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

public sealed class DeleteContentTool : AIFunction
{
    public const string TheName = "deleteContentItem";

    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public DeleteContentTool(
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

    public override string Description => "Permanently removes a content item from the site, including all of its versions.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.CloneContent))
        {
            return "You do not have permission to delete content items.";
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

        await _contentManager.RemoveAsync(contentItem);

        return "Content item was successfully deleted";
    }
}
