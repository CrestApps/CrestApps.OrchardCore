using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgenticCommerce.AI;

/// <summary>
/// Starts a checkout for items the buyer chose with the site's AI assistant, and returns the link the buyer pays at in
/// the store's own checkout. The assistant never pays.
/// </summary>
public sealed class StartStoreCheckoutTool : AgentCommerceToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "startStoreCheckout";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "items": {
              "type": "array",
              "description": "The items to buy.",
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string", "description": "The item id, as searchStoreProducts returns it for a variant." },
                  "quantity": { "type": "integer", "description": "How many. Defaults to 1." }
                },
                "required": ["id"]
              }
            },
            "email": { "type": "string", "description": "The buyer's e-mail address, when they gave it." }
          },
          "required": ["items"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Starts a checkout for the buyer's chosen items and returns its total and the link where the buyer enters their address and pays. Give the buyer the link; never ask for card details.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var lines = new List<AgentCheckoutLineInput>();

        if (arguments.TryGetValue("items", out var value) && JsonSerializer.SerializeToNode(value, JsonOptions) is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>())
            {
                lines.Add(new AgentCheckoutLineInput
                {
                    ItemId = AgentJson.String(item, "id"),
                    Quantity = Math.Max(1, AgentJson.Int(item, "quantity") ?? 1),
                });
            }
        }

        if (lines.Count == 0)
        {
            return "Give at least one item id.";
        }

        var services = arguments.Services;
        var email = Read(arguments, "email");
        var user = services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        var result = await services.GetRequiredService<IAgentCheckoutService>().CreateAsync(new AgentCheckoutContext
        {
            PlatformId = AssistantPlatformId,
            Protocol = AgenticCommerceConstants.Protocols.Assistant,
            UserId = user?.Identity?.IsAuthenticated == true ? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null,
        }, new AgentCheckoutInput
        {
            Lines = lines,
            Buyer = email is null ? null : new AgentBuyer { Email = email },
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Error;
        }

        var view = result.View;

        return new JsonObject
        {
            ["checkout_url"] = view.ContinueUrl,
            ["currency"] = view.Currency,
            ["total"] = view.Totals.Total,
            ["items"] = new JsonArray([.. view.Lines.Select(line => (JsonNode)new JsonObject { ["title"] = line.Title, ["quantity"] = line.Quantity, ["total"] = line.Total })]),
            ["problems"] = new JsonArray([.. view.Messages.Where(message => message.Type == AgentMessageType.Error && message.Code != AgentMessageCodes.Missing).Select(message => (JsonNode)message.Content)]),
        }.ToJsonString(JsonOptions);
    }
}
