using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agents.Contents;

public sealed class GetContentItemLinkTool : AIFunction
{
    public const string TheName = "getLinkForContentItem";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public GetContentItemLinkTool(
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "type": "object",
                "properties": {
                    "contentItemId": {
                        "type": "string",
                        "description": "The string representation of the content item's ContentItemId."
                    },
                    "type": {
                        "type": "string",
                        "description": "The type of link to generate.",
                        "enum": ["display", "edit"],
                        "default": "display"
                    }
                },
                "additionalProperties": false,
                "required": ["contentItemId"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Get a URL for the given content item based on the type.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("contentItemId", out var data))
        {
            return ValueTask.FromResult<object>("Unable to find a contentItemId argument in the function arguments.");
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

        var type = "display";

        if (arguments.TryGetValue("type", out var typeData))
        {
            type = ToolHelpers.GetStringValue(typeData);
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

        var link = _linkGenerator.GetUriByRouteValues(_httpContextAccessor.HttpContext, null, routeValues);

        return ValueTask.FromResult<object>(link);
    }
}
