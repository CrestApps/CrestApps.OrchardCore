using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

public sealed class UnpublishContentTool : AIFunction
{
    public const string TheName = "unpublishContentItem";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
        """);

    public override string Name => TheName;

    public override string Description => "Changes the status of a published content item to draft, making it editable without being publicly visible.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var contentManager = arguments.Services.GetRequiredService<IContentManager>();
        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, CommonPermissions.PublishContent))
        {
            return "You do not have permission to publish content items.";
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "Unable to find a contentItemId argument in the function arguments.";
        }

        var contentItem = await contentManager.GetAsync(contentItemId);

        if (contentItem is null)
        {
            return $"Unable to find a content item that match the ContentItemId: {contentItemId}";
        }

        await contentManager.UnpublishAsync(contentItem);

        return "Content item was successfully unpublished";
    }
}
