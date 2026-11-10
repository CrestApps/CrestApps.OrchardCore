using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// UCP's MCP binding: JSON-RPC over Streamable HTTP, answered as plain JSON, with UCP's checkout operations as tools.
/// Each tool takes the same checkout the REST binding does and returns it in <c>structuredContent</c>.
/// </summary>
public static class UcpMcpEndpoints
{
    /// <summary>
    /// The binding's path, after the tenant's base address.
    /// </summary>
    public const string Route = "ucp/mcp";

    private const string ProtocolVersion = "2025-06-18";

    /// <summary>
    /// Maps the MCP binding.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    public static IEndpointRouteBuilder MapUcpMcp(this IEndpointRouteBuilder routes)
    {
        routes.MapPost(Route, HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .AddEndpointFilter<UcpRequestFilter>()
            .WithName("UcpMcp");

        return routes;
    }

    private static async Task<IResult> HandleAsync(HttpContext httpContext, IAgentCheckoutService checkouts, UcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);

        if (request.Json is not JsonObject message || AgentJson.String(message, "method") is not { } method)
        {
            return Error(null, -32600, "Send a JSON-RPC request.");
        }

        var id = message["id"]?.DeepClone();

        // A notification gets no answer.
        if (id is null)
        {
            return Results.Accepted();
        }

        switch (method)
        {
            case "initialize":
                return Result(id, new JsonObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "ucp-shopping", ["version"] = request.Version },
                });

            case "ping":
                return Result(id, new JsonObject());

            case "tools/list":
                return Result(id, new JsonObject { ["tools"] = ListTools() });

            case "tools/call":
                return await CallAsync(id, message["params"] as JsonObject, request, checkouts, mapper, httpContext);
        }

        return Error(id, -32601, $"The method '{method}' isn't offered.");
    }

    private static async Task<IResult> CallAsync(
        JsonNode id,
        JsonObject parameters,
        AgentRequest request,
        IAgentCheckoutService checkouts,
        UcpCheckoutMapper mapper,
        HttpContext httpContext)
    {
        var name = AgentJson.String(parameters, "name");
        var arguments = parameters?["arguments"] as JsonObject ?? [];
        var checkoutId = AgentJson.String(arguments, "id");
        var checkout = arguments["checkout"] as JsonObject ?? [];
        var cancellationToken = httpContext.RequestAborted;

        AgentCheckoutResult result;

        switch (name)
        {
            case "create_checkout":
                result = await checkouts.CreateAsync(request.Context, UcpCheckoutMapper.ReadInput(checkout), cancellationToken);
                break;

            case "get_checkout":
                result = await checkouts.GetAsync(request.Context, checkoutId, cancellationToken);
                break;

            case "update_checkout":
                result = await checkouts.UpdateAsync(request.Context, checkoutId, UcpCheckoutMapper.ReadUpdate(checkout), cancellationToken);
                break;

            case "complete_checkout":
                if (UcpCheckoutMapper.ReadPayment(checkout) is not { } payment)
                {
                    return ToolResult(id, UcpCheckoutMapper.WriteError(request, "Give the payment instrument to complete the checkout with.", AgentMessageCodes.PaymentFailed), isError: true);
                }

                var update = UcpCheckoutMapper.ReadUpdate(checkout);

                update.Lines = null;
                result = await checkouts.CompleteAsync(request.Context, checkoutId, payment, update, cancellationToken);
                break;

            case "cancel_checkout":
                result = await checkouts.CancelAsync(request.Context, checkoutId, intentTrace: null, cancellationToken);
                break;

            default:
                return Error(id, -32602, $"There is no tool named '{name}'.");
        }

        if (result.Failure is AgentCheckoutFailure.NotFound or AgentCheckoutFailure.Busy or AgentCheckoutFailure.TooMany)
        {
            return Error(id, -32000, result.Error);
        }

        if (result.View is null)
        {
            return ToolResult(id, UcpCheckoutMapper.WriteError(request, result.Error), isError: true);
        }

        return ToolResult(id, await mapper.WriteAsync(request, result, cancellationToken), isError: false);
    }

    private static JsonArray ListTools()
    {
        var tools = new JsonArray();

        foreach (var (name, description, needsId, needsCheckout) in new[]
        {
            ("create_checkout", "Creates a checkout session for line items.", false, true),
            ("get_checkout", "Returns a checkout session.", true, false),
            ("update_checkout", "Replaces a checkout session's line items, buyer, fulfillment and discount codes.", true, true),
            ("complete_checkout", "Completes a checkout session with a payment instrument and places the order.", true, true),
            ("cancel_checkout", "Cancels a checkout session.", true, false),
        })
        {
            var properties = new JsonObject
            {
                ["meta"] = new JsonObject
                {
                    ["type"] = "object",
                    ["description"] = "The UCP-Agent profile and the idempotency key.",
                },
            };

            var required = new JsonArray();

            if (needsId)
            {
                properties["id"] = new JsonObject { ["type"] = "string", ["description"] = "The checkout session's id." };
                required.Add("id");
            }

            if (needsCheckout)
            {
                properties["checkout"] = new JsonObject { ["type"] = "object", ["description"] = "The checkout, as the REST binding takes it." };
                required.Add("checkout");
            }

            tools.Add(new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required,
                },
            });
        }

        return tools;
    }

    private static IResult ToolResult(JsonNode id, JsonObject structured, bool isError)
        => Result(id, new JsonObject
        {
            ["structuredContent"] = structured,
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = structured.ToJsonString(JsonSerializerOptions.Web),
            }),
            ["isError"] = isError,
        });

    private static IResult Result(JsonNode id, JsonNode result)
        => Results.Json(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result,
        });

    private static IResult Error(JsonNode id, int code, string message)
        => Results.Json(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message,
            },
        });
}
