using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Answers UCP's A2A binding: A2A JSON-RPC requests whose messages carry UCP actions and payloads in data parts. Each
/// action runs the same <see cref="UcpOperations"/> the REST binding runs, after <see cref="UcpRequestFilter"/> signed
/// the platform in and negotiated the version. The answer is an A2A message whose data part holds the UCP answer under
/// <c>a2a.ucp.checkout</c> (or <c>a2a.ucp.order</c>). An action that changes something runs once per
/// <c>messageId</c>, with UCP's idempotency rules, scoped to the platform and the action.
/// </summary>
public sealed class UcpA2aMessageHandler
{
    /// <summary>
    /// How long the agent remembers the checkout a conversation (an A2A <c>contextId</c>) is about: the longest a
    /// checkout lives.
    /// </summary>
    public static readonly TimeSpan ContextLifetime = TimeSpan.FromHours(72);

    private readonly UcpOperations _operations;
    private readonly IDistributedCache _cache;
    private readonly IDistributedLock _locks;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="UcpA2aMessageHandler"/> class.
    /// </summary>
    /// <param name="operations">UCP's operations.</param>
    /// <param name="cache">The distributed cache, for idempotent answers and conversations' checkouts.</param>
    /// <param name="locks">The distributed lock.</param>
    /// <param name="shellSettings">The tenant's settings.</param>
    public UcpA2aMessageHandler(UcpOperations operations, IDistributedCache cache, IDistributedLock locks, ShellSettings shellSettings)
    {
        _operations = operations;
        _cache = cache;
        _locks = locks;
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Answers a request <see cref="UcpRequestFilter"/> signed in.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    public async Task<IResult> HandleAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var request = AgentRequest.Get(httpContext);

        if (request.Json is null)
        {
            return Results.Json(UcpJsonRpc.Error(null, UcpJsonRpc.ParseError, "Send a JSON-RPC 2.0 request."));
        }

        if (request.Json is not JsonObject rpc || AgentJson.String(rpc, "method") is not { } method)
        {
            return Results.Json(UcpJsonRpc.Error((request.Json as JsonObject)?["id"], UcpJsonRpc.InvalidRequest, "Send a JSON-RPC 2.0 request with a method."));
        }

        var id = rpc["id"];

        // A notification gets no answer.
        if (id is null)
        {
            return Results.Accepted();
        }

        if (IsExtensionRequested(httpContext.Request.Headers[UcpA2a.ExtensionsHeader].ToString()))
        {
            httpContext.Response.Headers[UcpA2a.ExtensionsHeader] = UcpA2a.ExtensionUri(request.Version);
        }

        return Results.Json(await DispatchAsync(request, id, method, rpc["params"] as JsonObject, httpContext.RequestAborted));
    }

    /// <summary>
    /// Returns the JSON-RPC answer to a method.
    /// </summary>
    /// <param name="request">The signed-in request.</param>
    /// <param name="id">The JSON-RPC request's id.</param>
    /// <param name="method">The method.</param>
    /// <param name="parameters">The method's parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<JsonObject> DispatchAsync(AgentRequest request, JsonNode id, string method, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.Equals(method, UcpA2a.Methods.SendMessage, StringComparison.Ordinal))
        {
            return await SendAsync(request, id, parameters, cancellationToken);
        }

        if (string.Equals(method, UcpA2a.Methods.StreamMessage, StringComparison.Ordinal))
        {
            return UcpJsonRpc.Error(id, UcpA2a.ErrorCodes.UnsupportedOperation, "Streaming isn't offered. Use message/send.");
        }

        if (method.StartsWith(UcpA2a.Methods.PushNotificationPrefix, StringComparison.Ordinal))
        {
            return UcpJsonRpc.Error(id, UcpA2a.ErrorCodes.PushNotificationNotSupported, "Push notifications aren't offered.");
        }

        if (method.StartsWith(UcpA2a.Methods.TasksPrefix, StringComparison.Ordinal))
        {
            return UcpJsonRpc.Error(id, UcpA2a.ErrorCodes.TaskNotFound, "There is no such task. This agent answers with messages and keeps no tasks.");
        }

        return UcpJsonRpc.Error(id, UcpJsonRpc.MethodNotFound, $"The method '{method}' isn't offered.");
    }

    /// <summary>
    /// Returns whether an <c>X-A2A-Extensions</c> header activates UCP's extension, in any version the store serves.
    /// </summary>
    /// <param name="header">The header's value.</param>
    public static bool IsExtensionRequested(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var requested = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return UcpConstants.Versions.Any(version => requested.Contains(UcpA2a.ExtensionUri(version), StringComparer.Ordinal));
    }

    private async Task<JsonObject> SendAsync(AgentRequest request, JsonNode id, JsonObject parameters, CancellationToken cancellationToken)
    {
        var message = UcpA2aMessage.Parse(parameters?["message"]);

        if (message is null)
        {
            return UcpJsonRpc.Error(id, UcpJsonRpc.InvalidParams, "Send the message in params.message.");
        }

        if (message.MessageId is null)
        {
            return UcpJsonRpc.Error(id, UcpJsonRpc.InvalidParams, "Give the message a messageId.");
        }

        var contextId = message.ContextId ?? Guid.NewGuid().ToString();

        // The agent takes structured UCP requests only; it has no model to read a buyer's words with.
        if (!message.HasData)
        {
            return UcpJsonRpc.Result(id, Reply(Guid.NewGuid().ToString(), contextId, new JsonObject
            {
                ["kind"] = "text",
                ["text"] = $"Send a UCP action in a data part: {string.Join(", ", UcpA2a.Actions.All)}.",
            }));
        }

        var action = message.Action;

        if (string.IsNullOrEmpty(action))
        {
            return UcpJsonRpc.Error(id, UcpJsonRpc.InvalidParams, "Name the UCP action in a data part's action member.");
        }

        if (!UcpA2a.Actions.All.Contains(action, StringComparer.Ordinal))
        {
            return UcpJsonRpc.Error(id, UcpJsonRpc.InvalidParams, $"The action '{action}' isn't offered. Use {string.Join(", ", UcpA2a.Actions.All)}.");
        }

        if (!UcpA2a.Actions.IsMutating(action))
        {
            var read = await RunAsync(request, message, contextId, action, cancellationToken);

            return Write(id, Guid.NewGuid().ToString(), contextId, action, read);
        }

        if (message.MessageId.Length > UcpA2a.MaxMessageIdLength)
        {
            return UcpJsonRpc.Error(id, UcpJsonRpc.InvalidParams, $"A messageId is at most {UcpA2a.MaxMessageIdLength} characters.");
        }

        // A retry of the same message gets the first answer; the same messageId with another message is a conflict.
        var run = await IdempotentOperation.RunAsync(
            _cache,
            _locks,
            $"ucp-a2a|{_shellSettings?.Name}|{request.Platform?.ItemId}|{action}",
            message.MessageId,
            message.GetFingerprint(),
            UcpEndpoints.Idempotency.Lifetime,
            async token =>
            {
                var outcome = await RunAsync(request, message, contextId, action, token);
                var stored = StoredReply.Write(Guid.NewGuid().ToString(), contextId, outcome);

                return new IdempotentOperationAnswer(stored.ToJsonString(), outcome.IsFinal);
            },
            cancellationToken);

        switch (run.Outcome)
        {
            case IdempotentOperationOutcome.Conflict:
                return UcpJsonRpc.TransportError(id, UcpEndpoints.IdempotencyConflictCode, "This messageId was used for another message.");

            case IdempotentOperationOutcome.InFlight:
                return UcpJsonRpc.TransportError(id, UcpEndpoints.IdempotencyInFlightCode, "A message with this messageId is still running. Try again in a moment.", retryAfter: 1);
        }

        var reply = StoredReply.Read(run.Value);

        return Write(id, reply.MessageId, reply.ContextId, action, reply.Outcome);
    }

    private async Task<UcpOperationResult> RunAsync(AgentRequest request, UcpA2aMessage message, string contextId, string action, CancellationToken cancellationToken)
    {
        if (action == UcpA2a.Actions.GetOrder)
        {
            return await _operations.GetOrderAsync(request, message.OrderId, cancellationToken);
        }

        if (action == UcpA2a.Actions.CreateCheckout)
        {
            return await RememberAsync(request, contextId, await _operations.CreateCheckoutAsync(request, message.ToCheckoutBody(), cancellationToken));
        }

        if (action == UcpA2a.Actions.AddToCheckout)
        {
            return await RememberAsync(request, contextId, await AddAsync(request, message, contextId, cancellationToken));
        }

        var checkoutId = message.CheckoutId ?? await RecallAsync(request, contextId, cancellationToken);

        if (checkoutId is null)
        {
            return UcpOperationResult.Fail(StatusCodes.Status400BadRequest, UcpOperationResult.InvalidRequestCode, "Give the checkout's id, or continue the conversation (contextId) that created it.");
        }

        var result = action switch
        {
            UcpA2a.Actions.GetCheckout => await _operations.GetCheckoutAsync(request, checkoutId, cancellationToken),
            UcpA2a.Actions.UpdateCheckout => await _operations.UpdateCheckoutAsync(request, checkoutId, message.ToCheckoutBody(), cancellationToken),
            UcpA2a.Actions.CompleteCheckout => await _operations.CompleteCheckoutAsync(request, checkoutId, message.ToCheckoutBody(), cancellationToken),
            _ => await _operations.CancelCheckoutAsync(request, checkoutId, cancellationToken),
        };

        return await RememberAsync(request, contextId, result);
    }

    // UCP's binding shows a product added to the conversation's checkout: the checkout gets one more of it, or starts
    // with it when the conversation has no open checkout.
    private async Task<UcpOperationResult> AddAsync(AgentRequest request, UcpA2aMessage message, string contextId, CancellationToken cancellationToken)
    {
        var itemId = AgentJson.String(message.Data, "product_id") ?? AgentJson.String(message.Data, "item_id");
        var quantity = AgentJson.Int(message.Data, "quantity") ?? 1;

        if (string.IsNullOrWhiteSpace(itemId) || quantity < 1)
        {
            return UcpOperationResult.Fail(StatusCodes.Status400BadRequest, UcpOperationResult.InvalidRequestCode, "Give the product_id to add, and a quantity of at least 1.");
        }

        var checkoutId = message.CheckoutId ?? await RecallAsync(request, contextId, cancellationToken);

        if (checkoutId is not null)
        {
            var current = await _operations.GetCheckoutAsync(request, checkoutId, cancellationToken);

            if (current.IsTransportError && current.ErrorCode != UcpOperationResult.NotFoundCode)
            {
                return current;
            }

            if (!current.IsTransportError && IsOpen(current.Body))
            {
                var lines = AddLine(current.Body["line_items"] as JsonArray, itemId, quantity);

                return await _operations.UpdateCheckoutAsync(request, checkoutId, new JsonObject { ["line_items"] = lines }, cancellationToken);
            }
        }

        var body = message.ToCheckoutBody();

        body["line_items"] = AddLine(body["line_items"] as JsonArray, itemId, quantity);

        return await _operations.CreateCheckoutAsync(request, body, cancellationToken);
    }

    /// <summary>
    /// Returns line items with one more product: its quantity raised on the line that has it, or a new line.
    /// </summary>
    /// <param name="lines">The current line items, as UCP writes them.</param>
    /// <param name="itemId">The product's item id.</param>
    /// <param name="quantity">How many to add.</param>
    public static JsonArray AddLine(JsonArray lines, string itemId, int quantity)
    {
        var result = new JsonArray();
        var added = false;

        foreach (var line in (lines ?? []).OfType<JsonObject>())
        {
            var lineItemId = AgentJson.String(line["item"], "id");

            if (lineItemId is null)
            {
                continue;
            }

            var lineQuantity = AgentJson.Int(line, "quantity") ?? 1;

            if (!added && string.Equals(lineItemId, itemId, StringComparison.Ordinal))
            {
                lineQuantity += quantity;
                added = true;
            }

            result.Add(new JsonObject
            {
                ["item"] = new JsonObject { ["id"] = lineItemId },
                ["quantity"] = lineQuantity,
            });
        }

        if (!added)
        {
            result.Add(new JsonObject
            {
                ["item"] = new JsonObject { ["id"] = itemId },
                ["quantity"] = quantity,
            });
        }

        return result;
    }

    private static bool IsOpen(JsonObject checkout)
        => AgentJson.String(checkout?["ucp"], "status") != "error" &&
            AgentJson.String(checkout, "status") is "incomplete" or "ready_for_complete" or "requires_escalation";

    // The conversation remembers the checkout it is about, so a platform can continue it without repeating its id.
    private async Task<UcpOperationResult> RememberAsync(AgentRequest request, string contextId, UcpOperationResult result)
    {
        if (!result.IsTransportError && AgentJson.String(result.Body, "id") is { } checkoutId)
        {
            await _cache.SetStringAsync(GetContextKey(request, contextId), checkoutId, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ContextLifetime,
            });
        }

        return result;
    }

    private Task<string> RecallAsync(AgentRequest request, string contextId, CancellationToken cancellationToken)
        => _cache.GetStringAsync(GetContextKey(request, contextId), cancellationToken);

    private string GetContextKey(AgentRequest request, string contextId)
        => $"ucp-a2a-context|{_shellSettings?.Name}|{request.Platform?.ItemId}|{contextId}";

    private static JsonObject Write(JsonNode id, string messageId, string contextId, string action, UcpOperationResult outcome)
    {
        if (outcome.IsTransportError)
        {
            return UcpJsonRpc.TransportError(id, outcome.ErrorCode, outcome.Error, outcome.RetryAfter);
        }

        var key = action == UcpA2a.Actions.GetOrder ? UcpA2a.DataKeys.Order : UcpA2a.DataKeys.Checkout;

        return UcpJsonRpc.Result(id, Reply(messageId, contextId, new JsonObject
        {
            ["kind"] = "data",
            ["data"] = new JsonObject
            {
                [key] = outcome.Body.DeepClone(),
            },
        }));
    }

    private static JsonObject Reply(string messageId, string contextId, JsonObject part)
        => new()
        {
            ["kind"] = "message",
            ["role"] = "agent",
            ["messageId"] = messageId,
            ["contextId"] = contextId,
            ["parts"] = new JsonArray(part),
        };

    // What is kept for a retry: the answer, and the ids it was sent with, so a replay is the same message.
    private sealed record StoredReply(string MessageId, string ContextId, UcpOperationResult Outcome)
    {
        public static JsonObject Write(string messageId, string contextId, UcpOperationResult outcome)
        {
            var json = new JsonObject
            {
                ["message_id"] = messageId,
                ["context_id"] = contextId,
                ["status"] = outcome.StatusCode,
            };

            if (outcome.IsTransportError)
            {
                json["error_code"] = outcome.ErrorCode;
                json["error"] = outcome.Error;

                if (outcome.RetryAfter is { } seconds)
                {
                    json["retry_after"] = seconds;
                }
            }
            else
            {
                json["body"] = outcome.Body.DeepClone();
            }

            return json;
        }

        public static StoredReply Read(string value)
        {
            var json = JsonNode.Parse(value).AsObject();
            var status = AgentJson.Int(json, "status") ?? StatusCodes.Status200OK;
            var outcome = json["body"] is JsonObject body
                ? UcpOperationResult.Answer((JsonObject)body.DeepClone(), status)
                : UcpOperationResult.Fail(status, AgentJson.String(json, "error_code"), AgentJson.String(json, "error"), AgentJson.Int(json, "retry_after"));

            return new StoredReply(AgentJson.String(json, "message_id"), AgentJson.String(json, "context_id"), outcome);
        }
    }
}
