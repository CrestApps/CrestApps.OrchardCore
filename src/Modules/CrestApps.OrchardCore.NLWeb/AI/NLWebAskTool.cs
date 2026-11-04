using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Protocol;
using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// The NLWeb <c>ask</c> tool: asks the site a question in natural language and returns the NLWeb response, with the
/// matching content items as schema.org objects. It is an AI tool, so the MCP server, WebMCP and AI profiles can
/// offer it, and it answers as the user who calls it.
/// </summary>
public sealed class NLWebAskTool : AIFunction
{
    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "object",
          "description": "The question and its filters.",
          "properties": {
            "text": {
              "type": "string",
              "description": "The question, in natural language."
            },
            "site": {
              "type": "string",
              "description": "The name of the part of the site to search. Leave it out to search the default part."
            },
            "itemType": {
              "type": "string",
              "description": "The schema.org type of the items wanted, such as Product, Recipe, Event or Article."
            }
          },
          "required": [
            "text"
          ]
        },
        "context": {
          "type": "object",
          "description": "The conversation's context.",
          "properties": {
            "prev": {
              "type": "array",
              "items": {
                "type": "string"
              },
              "description": "The earlier questions of the conversation, oldest first."
            }
          }
        },
        "prefer": {
          "type": "object",
          "description": "How the answer should be returned.",
          "properties": {
            "mode": {
              "type": "string",
              "enum": [
                "list",
                "summarize",
                "generate"
              ],
              "description": "list returns the matching items; summarize also summarizes them; generate also answers the question from them."
            }
          }
        }
      },
      "required": [
        "query"
      ]
    }
    """);

    /// <inheritdoc/>
    public override string Name => NLWebConstants.AskToolName;

    /// <inheritdoc/>
    public override string Description => "Asks this web site a question in natural language (NLWeb) and returns the matching content items as schema.org objects, with their URLs, and, when asked, a summary or an answer written from them.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var user = arguments.Services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var builder = new NLWebResponseBuilder();

        if (!await authorizationService.AuthorizeAsync(user, NLWebPermissionsProvider.QueryNLWeb))
        {
            builder.Add(new NLWebFailureEvent(NLWebProtocol.ErrorCodes.Forbidden, "You may not ask this site questions."));

            return builder.Build().ToJsonString(NLWebJson.SerializerOptions);
        }

        if (!NLWebRequestReader.TryRead(ToJsonObject(arguments), out var request, out var error))
        {
            builder.Add(new NLWebFailureEvent(NLWebProtocol.ErrorCodes.InvalidRequest, error));

            return builder.Build().ToJsonString(NLWebJson.SerializerOptions);
        }

        var askService = arguments.Services.GetRequiredService<INLWebAskService>();

        await foreach (var nlwebEvent in askService.AskAsync(request, user, cancellationToken))
        {
            builder.Add(nlwebEvent);
        }

        return builder.Build().ToJsonString(NLWebJson.SerializerOptions);
    }

    /// <summary>
    /// Turns the tool arguments into the JSON body of an NLWeb request.
    /// </summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <returns>The JSON object.</returns>
    internal static JsonObject ToJsonObject(IEnumerable<KeyValuePair<string, object>> arguments)
    {
        var body = new JsonObject();

        foreach (var argument in arguments)
        {
            body[argument.Key] = argument.Value switch
            {
                null => null,
                JsonNode node => node.DeepClone(),
                JsonElement element => JsonNode.Parse(element.GetRawText()),
                _ => JsonSerializer.SerializeToNode(argument.Value),
            };
        }

        return body;
    }
}
