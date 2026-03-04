using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
              "enum": ["display", "edit"],
              "default": "display"
            }
          },
          "required": ["contentItemId"],
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

        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var linkGenerator = arguments.Services.GetRequiredService<LinkGenerator>();

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "Unable to find a contentItemId argument in the function arguments.";
        }

        var contentManager = arguments.Services.GetRequiredService<IContentManager>();

        var type = arguments.GetFirstValueOrDefault("type", "display");

        var contentItem = await contentManager.GetAsync(contentItemId);

        if (contentItem is not null)
        {
            var metadata = await contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);

            if (type == "edit" && metadata.AdminRouteValues is not null)
            {
                return linkGenerator.GetUriByRouteValues(httpContextAccessor.HttpContext, null, metadata.AdminRouteValues);
            }
            else if (metadata.DisplayRouteValues is not null)
            {
                return linkGenerator.GetUriByRouteValues(httpContextAccessor.HttpContext, null, metadata.DisplayRouteValues);
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

        var link = linkGenerator.GetUriByRouteValues(httpContextAccessor.HttpContext, null, routeValues);

        if (string.IsNullOrEmpty(link))
        {
            return "Unable to generate a link for the given content item.";
        }

        return link;
    }
}
