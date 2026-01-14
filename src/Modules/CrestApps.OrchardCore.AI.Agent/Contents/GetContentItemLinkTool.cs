using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

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

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services, nameof(arguments.Services));

        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var linkGenerator = arguments.Services.GetRequiredService<LinkGenerator>();

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return ValueTask.FromResult<object>("Unable to find a contentItemId argument in the function arguments.");
        }

        var type = arguments.GetFirstValueOrDefault("type", "display");

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

        return ValueTask.FromResult<object>(link);
    }
}
