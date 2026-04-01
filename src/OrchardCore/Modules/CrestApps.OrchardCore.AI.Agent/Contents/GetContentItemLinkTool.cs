using System.Text.Json;
using CrestApps.AI.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

public sealed class GetContentItemLinkTool : AIFunction
{
    public const string TheName = "getLinkForContentItem";
    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The unique identifier of the content item, represented as a string (ContentItemId)."
        },
        "type": {
          "type": "string",
          "description": "Specifies the type of link to generate.",
          "enum": [
            "display",
            "edit"
          ],
          "default": "display"
        }
      },
      "required": [
        "contentItemId"
      ],
      "additionalProperties": false
    }
    """);
    public override string Name => TheName;
    public override string Description => "Get a URL for the given content item based on the type.";
    public override JsonElement JsonSchema => _jsonSchema;
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected async override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<GetContentItemLinkTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            logger.LogWarning("AI tool '{ToolName}': Unable to find a contentItemId argument in the function arguments.", TheName);

            return "Unable to find a contentItemId argument in the function arguments.";
        }

        // HttpContext may be null when invoked from a background task (e.g., post-session processing).
        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}': HttpContext is null (likely running in a background task). Returning content item ID only.", TheName);
            }

            return $"Unable to generate a URL because the request context is not available (background execution). The content item ID is '{contentItemId}'.";
        }

        var linkGenerator = arguments.Services.GetRequiredService<LinkGenerator>();
        var contentManager = arguments.Services.GetRequiredService<IContentManager>();
        var type = arguments.GetFirstValueOrDefault("type", "display");
        var contentItem = await contentManager.GetAsync(contentItemId);

        if (contentItem is not null)
        {
            var metadata = await contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);

            if (type == "edit" && metadata.AdminRouteValues is not null)
            {
                return linkGenerator.GetUriByRouteValues(httpContext, null, metadata.AdminRouteValues);
            }
            else if (metadata.DisplayRouteValues is not null)
            {
                return linkGenerator.GetUriByRouteValues(httpContext, null, metadata.DisplayRouteValues);
            }
        }

        var routeValues = type switch
        {
            "edit" => new RouteValueDictionary()
            {
                { "Area", "OrchardCore.Contents" },
                { "Controller", "Admin" },
                { "Action", "Edit" },
                { "contentItemId", contentItemId },
            },
            _ => new RouteValueDictionary()
            {
                { "Area", "OrchardCore.Contents" },
                { "Controller", "Admin" },
                { "Action", "Display" },
                { "contentItemId", contentItemId },
            },
        };

        var link = linkGenerator.GetUriByRouteValues(httpContext, null, routeValues);

        if (string.IsNullOrEmpty(link))
        {
            logger.LogWarning("AI tool '{ToolName}': Unable to generate a link for content item '{ContentItemId}'.", TheName, contentItemId);

            return "Unable to generate a link for the given content item.";
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
        }

        return link;
    }
}
