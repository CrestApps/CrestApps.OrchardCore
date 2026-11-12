using OrchardCore.RateLimits;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// ACP's MCP binding: JSON-RPC 2.0 over MCP Streamable HTTP, answered as plain JSON, with ACP's checkout session
/// operations as tools. Each tool takes <c>{ meta, id?, payload? }</c>, where <c>payload</c> is the REST request body,
/// runs the REST binding's code (idempotency and request signatures included) and returns the REST answer in
/// <c>structuredContent</c>. An ACP error is a JSON-RPC error <c>-32000</c> with the ACP error in <c>data</c>; only
/// malformed arguments get <c>-32602</c>. The platform signs in on the HTTP request, as it does for REST.
/// </summary>
public static class AcpMcpEndpoints
{
    /// <summary>
    /// The binding's path, after the tenant's base address.
    /// </summary>
    public const string Route = "acp/mcp";

    /// <summary>
    /// The JSON-RPC error code of an ACP error.
    /// </summary>
    public const int AcpErrorCode = -32000;

    /// <summary>
    /// The JSON-RPC error code of malformed parameters.
    /// </summary>
    public const int InvalidParamsCode = -32602;

    private const string ProtocolVersion = "2025-06-18";

    /// <summary>
    /// The names of ACP's MCP tools.
    /// </summary>
    public static class Tools
    {
        /// <summary>
        /// Creates a checkout session.
        /// </summary>
        public const string Create = "create_checkout_session";

        /// <summary>
        /// Returns a checkout session.
        /// </summary>
        public const string Get = "get_checkout_session";

        /// <summary>
        /// Changes a checkout session.
        /// </summary>
        public const string Update = "update_checkout_session";

        /// <summary>
        /// Pays for a checkout session and places the order.
        /// </summary>
        public const string Complete = "complete_checkout_session";

        /// <summary>
        /// Cancels a checkout session.
        /// </summary>
        public const string Cancel = "cancel_checkout_session";

        /// <summary>
        /// Every tool, in the order <c>tools/list</c> gives them.
        /// </summary>
        public static readonly IReadOnlyList<string> All = [Create, Get, Update, Complete, Cancel];
    }

    /// <summary>
    /// Maps the MCP binding.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    public static IEndpointRouteBuilder MapAcpMcp(this IEndpointRouteBuilder routes)
    {
        routes.MapPost(Route, HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName("AcpMcp").WithRateLimitGroup(AgenticCommerceConstants.RateLimitGroups.Checkout);

        return routes;
    }

    private static async Task<IResult> HandleAsync(HttpContext httpContext, AcpCheckoutOperations operations, AcpMcpIdempotency idempotency)
    {
        var body = await AgentRequestBody.ReadAsync(httpContext.Request);

        if (body is null)
        {
            return Results.Json(
                AcpError(null, AcpResults.ErrorBody(StatusCodes.Status413PayloadTooLarge, "request_too_large", "The request is too large.")),
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var message = AgentRequestBody.Parse(body) as JsonObject;
        var id = message?["id"]?.DeepClone();

        // The connection signs in, as the REST binding does; authorization isn't part of a tool's arguments.
        var (request, signInError) = await AcpRequestFilter.SignInAsync(httpContext, body, AcpConstants.Versions[AcpConstants.Versions.Count - 1]);

        if (signInError is not null)
        {
            return Results.Json(AcpError(id, signInError.Body), statusCode: signInError.StatusCode);
        }

        request.Set(httpContext);
        httpContext.User = AgentPrincipal.Create(request.Platform.ItemId);

        if (message is null || AgentJson.String(message, "method") is not { } method)
        {
            return Results.Json(Error(null, -32600, "Send a JSON-RPC request."));
        }

        // A notification gets no answer.
        if (id is null)
        {
            return Results.Accepted();
        }

        switch (method)
        {
            case "initialize":
                return Results.Json(Result(id, new JsonObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "acp-checkout", ["version"] = request.Version },
                }));

            case "ping":
                return Results.Json(Result(id, new JsonObject()));

            case "tools/list":
                return Results.Json(Result(id, new JsonObject { ["tools"] = ListTools() }));

            case "tools/call":
                return Results.Json(await CallAsync(id, message["params"] as JsonObject, request, operations, idempotency, httpContext));
        }

        return Results.Json(Error(id, -32601, $"The method '{method}' isn't offered."));
    }

    private static async Task<JsonObject> CallAsync(
        JsonNode id,
        JsonObject parameters,
        AgentRequest request,
        AcpCheckoutOperations operations,
        AcpMcpIdempotency idempotency,
        HttpContext httpContext)
    {
        if (!TryReadCall(parameters, out var call, out var error))
        {
            return Error(id, InvalidParamsCode, error);
        }

        if (AcpRequestFilter.CheckVersion(call.ApiVersion) is { } versionError)
        {
            return AcpError(id, versionError);
        }

        // The payload is signed as the REST body is, so one signature serves both bindings.
        // Version 2 binds the signature to the tool, as the method "MCP" and the tool's name as the path.
        var signatureError = AcpSignatureCheck.Check(
            request.Platform,
            AcpRequestSignature.GetSigningInput(call.Payload),
            call.Signature,
            call.Timestamp,
            AcpSignatureCheck.McpMethod,
            call.Tool,
            AcpRequestFilter.GetNow(httpContext));

        if (signatureError is not null)
        {
            return AcpError(id, AcpResults.ErrorBody(StatusCodes.Status401Unauthorized, AcpSignatureCheck.ErrorCode, signatureError));
        }

        if (!await AcpSignatureCheck.TryUseAsync(httpContext, request.Platform, call.Signature, call.IdempotencyKey))
        {
            return AcpError(id, AcpResults.ErrorBody(StatusCodes.Status401Unauthorized, AcpSignatureCheck.ErrorCode, "The signature was used before. Sign each request anew."));
        }

        var version = call.ApiVersion.Trim();

        request.Version = version;
        request.Context.ProtocolVersion = version;
        request.Json = call.Payload;

        var cancellationToken = httpContext.RequestAborted;

        Task<AcpOperationResult> RunAsync()
            => call.Tool switch
            {
                Tools.Create => operations.CreateAsync(request, call.Payload, cancellationToken),
                Tools.Get => operations.GetAsync(request, call.Id, cancellationToken),
                Tools.Update => operations.UpdateAsync(request, call.Id, call.Payload, cancellationToken),
                Tools.Complete => operations.CompleteAsync(request, call.Id, call.Payload, cancellationToken),
                _ => operations.CancelAsync(request, call.Id, call.Payload, cancellationToken),
            };

        if (!call.IsMutating)
        {
            return ToResponse(id, await RunAsync(), call.RequestId, replayed: false);
        }

        var (result, replayed) = await idempotency.RunAsync(request.Platform.ItemId, call.Tool, call.Id, call.IdempotencyKey, call.Payload, RunAsync, cancellationToken);

        return ToResponse(id, result, call.RequestId, replayed);
    }

    /// <summary>
    /// Reads a <c>tools/call</c> request's parameters.
    /// </summary>
    /// <param name="parameters">The <c>params</c> object: <c>{ name, arguments: { meta, id?, payload? } }</c>.</param>
    /// <param name="call">The call.</param>
    /// <param name="error">Why the parameters are malformed.</param>
    /// <returns><see langword="false"/> when the parameters are malformed, which is a <c>-32602</c> error.</returns>
    public static bool TryReadCall(JsonObject parameters, out AcpMcpCall call, out string error)
    {
        call = null;

        var name = AgentJson.String(parameters, "name");

        if (string.IsNullOrEmpty(name))
        {
            error = "Name the tool to call.";

            return false;
        }

        if (!Tools.All.Contains(name, StringComparer.Ordinal))
        {
            error = $"There is no tool named '{name}'.";

            return false;
        }

        if (parameters["arguments"] is not JsonObject arguments)
        {
            error = "Send the tool's arguments as an object: { meta, id, payload }.";

            return false;
        }

        if (arguments["meta"] is not JsonObject meta)
        {
            error = "Send meta with api_version.";

            return false;
        }

        var apiVersion = AgentJson.String(meta, "api_version");

        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            error = "Send meta.api_version.";

            return false;
        }

        var checkoutId = AgentJson.String(arguments, "id");

        if (name != Tools.Create && string.IsNullOrWhiteSpace(checkoutId))
        {
            error = "Send the checkout session's id.";

            return false;
        }

        var payload = arguments["payload"];

        if (payload is not null and not JsonObject)
        {
            error = "Send payload as an object.";

            return false;
        }

        if (payload is null && name is Tools.Create or Tools.Update or Tools.Complete)
        {
            error = "Send the payload, as the REST binding's request body.";

            return false;
        }

        call = new AcpMcpCall
        {
            Tool = name,
            Id = name == Tools.Create ? null : checkoutId,
            Payload = payload as JsonObject,
            ApiVersion = apiVersion,
            IdempotencyKey = AgentJson.String(meta, "idempotency_key"),
            RequestId = AgentJson.String(meta, "request_id"),
            Signature = AgentJson.String(meta, "signature"),
            Timestamp = AgentJson.String(meta, "timestamp"),
        };

        error = null;

        return true;
    }

    /// <summary>
    /// Returns the JSON-RPC response to a tool call: the checkout session in <c>structuredContent</c> on success, or a
    /// <c>-32000</c> error with ACP's error in <c>data</c>.
    /// </summary>
    /// <param name="id">The JSON-RPC request's id.</param>
    /// <param name="result">What the operation answered.</param>
    /// <param name="requestId">The call's <c>meta.request_id</c>, echoed in <c>_meta</c>.</param>
    /// <param name="replayed">Whether the answer is a replay of the first call with the idempotency key.</param>
    public static JsonObject ToResponse(JsonNode id, AcpOperationResult result, string requestId = null, bool replayed = false)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Succeeded)
        {
            return AcpError(id, result.Body);
        }

        var body = result.Body ?? [];
        var toolResult = new JsonObject
        {
            ["structuredContent"] = body,
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = body.ToJsonString(JsonSerializerOptions.Web),
            }),
            ["isError"] = false,
        };

        if (!string.IsNullOrEmpty(requestId) || replayed)
        {
            var meta = new JsonObject();

            AgentJson.AddIf(meta, "request_id", requestId);

            if (replayed)
            {
                meta["idempotent_replayed"] = true;
            }

            toolResult["_meta"] = meta;
        }

        return Result(id, toolResult);
    }

    /// <summary>
    /// Returns a JSON-RPC <c>-32000</c> error that carries an ACP error object in <c>data</c>.
    /// </summary>
    /// <param name="id">The JSON-RPC request's id.</param>
    /// <param name="acpError">ACP's flat error <c>{ type, code, message, param }</c>.</param>
    public static JsonObject AcpError(JsonNode id, JsonObject acpError)
        => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject
            {
                ["code"] = AcpErrorCode,
                ["message"] = AgentJson.String(acpError, "message") ?? "The request failed.",
                ["data"] = acpError?.DeepClone(),
            },
        };

    private static JsonArray ListTools()
    {
        var tools = new JsonArray();

        foreach (var (name, description) in new[]
        {
            (Tools.Create, "Creates a checkout session for line items. payload is a CheckoutSessionCreateRequest."),
            (Tools.Get, "Returns a checkout session."),
            (Tools.Update, "Changes a checkout session's items, buyer, fulfillment or discount codes. payload is a CheckoutSessionUpdateRequest."),
            (Tools.Complete, "Pays for a checkout session with payment_data and places the order. payload is a CheckoutSessionCompleteRequest."),
            (Tools.Cancel, "Cancels a checkout session. payload is an optional CancelSessionRequest."),
        })
        {
            var properties = new JsonObject
            {
                ["meta"] = new JsonObject
                {
                    ["type"] = "object",
                    ["description"] = "The values the REST binding sends as headers.",
                    ["properties"] = new JsonObject
                    {
                        ["api_version"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JsonArray([.. AcpConstants.Versions.Select(version => (JsonNode)version)]),
                        },
                        ["idempotency_key"] = new JsonObject { ["type"] = "string", ["maxLength"] = AcpEndpoints.Idempotency.MaxKeyLength },
                        ["request_id"] = new JsonObject { ["type"] = "string" },
                        ["signature"] = new JsonObject { ["type"] = "string", ["description"] = "The base64url signature of the payload's canonical JSON." },
                        ["timestamp"] = new JsonObject { ["type"] = "string", ["format"] = "date-time" },
                    },
                    ["required"] = new JsonArray("api_version"),
                    ["additionalProperties"] = true,
                },
            };

            var required = new JsonArray("meta");

            if (name != Tools.Create)
            {
                properties["id"] = new JsonObject { ["type"] = "string", ["description"] = "The checkout session's id." };
                required.Add("id");
            }

            if (name != Tools.Get)
            {
                properties["payload"] = new JsonObject { ["type"] = "object", ["description"] = "The request body, as the REST binding takes it." };

                if (name != Tools.Cancel)
                {
                    required.Add("payload");
                }
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

    private static JsonObject Result(JsonNode id, JsonNode result)
        => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = result,
        };

    private static JsonObject Error(JsonNode id, int code, string message)
        => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message,
            },
        };
}
