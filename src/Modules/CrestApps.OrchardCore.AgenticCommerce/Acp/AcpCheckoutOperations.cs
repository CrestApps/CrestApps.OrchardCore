using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// ACP's checkout session operations, shared by the REST and the MCP bindings: each takes the request's payload (the
/// REST request body) and returns what the REST binding answers.
/// </summary>
public sealed class AcpCheckoutOperations
{
    private readonly IAgentCheckoutService _checkouts;
    private readonly AcpCheckoutMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AcpCheckoutOperations"/> class.
    /// </summary>
    /// <param name="checkouts">The agent checkout service.</param>
    /// <param name="mapper">The ACP checkout mapper.</param>
    public AcpCheckoutOperations(IAgentCheckoutService checkouts, AcpCheckoutMapper mapper)
    {
        _checkouts = checkouts;
        _mapper = mapper;
    }

    /// <summary>
    /// Creates a checkout session.
    /// </summary>
    /// <param name="request">The signed-in request.</param>
    /// <param name="payload">A <c>CheckoutSessionCreateRequest</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<AcpOperationResult> CreateAsync(AgentRequest request, JsonNode payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (payload is not JsonObject body)
        {
            return AcpOperationResult.Error(StatusCodes.Status400BadRequest, "invalid", "Send the checkout as a JSON object.");
        }

        var result = await _checkouts.CreateAsync(request.Context, AcpCheckoutMapper.ReadInput(body), cancellationToken);

        return result.Succeeded
            ? AcpOperationResult.Success(await _mapper.WriteAsync(request, result, cancellationToken), StatusCodes.Status201Created)
            : Fail(result);
    }

    /// <summary>
    /// Returns a checkout session.
    /// </summary>
    /// <param name="request">The signed-in request.</param>
    /// <param name="id">The checkout session's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<AcpOperationResult> GetAsync(AgentRequest request, string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _checkouts.GetAsync(request.Context, id, cancellationToken);

        return result.Succeeded
            ? AcpOperationResult.Success(await _mapper.WriteAsync(request, result, cancellationToken))
            : Fail(result);
    }

    /// <summary>
    /// Changes a checkout session.
    /// </summary>
    /// <param name="request">The signed-in request.</param>
    /// <param name="id">The checkout session's id.</param>
    /// <param name="payload">A <c>CheckoutSessionUpdateRequest</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<AcpOperationResult> UpdateAsync(AgentRequest request, string id, JsonNode payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (payload is not JsonObject body)
        {
            return AcpOperationResult.Error(StatusCodes.Status400BadRequest, "invalid", "Send the changes as a JSON object.");
        }

        var current = await _checkouts.GetAsync(request.Context, id, cancellationToken);

        if (!current.Succeeded)
        {
            return Fail(current);
        }

        var result = await _checkouts.UpdateAsync(request.Context, id, AcpCheckoutMapper.ReadUpdate(body, current.View), cancellationToken);

        return result.Succeeded
            ? AcpOperationResult.Success(await _mapper.WriteAsync(request, result, cancellationToken))
            : Fail(result);
    }

    /// <summary>
    /// Pays for a checkout session and places the order.
    /// </summary>
    /// <param name="request">The signed-in request.</param>
    /// <param name="id">The checkout session's id.</param>
    /// <param name="payload">A <c>CheckoutSessionCompleteRequest</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<AcpOperationResult> CompleteAsync(AgentRequest request, string id, JsonNode payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (payload is not JsonObject body)
        {
            return AcpOperationResult.Error(StatusCodes.Status400BadRequest, "invalid", "Send the payment as a JSON object.");
        }

        if (AcpCheckoutMapper.ReadPayment(body) is not { } payment || string.IsNullOrEmpty(payment.Credential))
        {
            return AcpOperationResult.Error(StatusCodes.Status400BadRequest, "missing", "Send payment_data with the payment handler and its credential.", "$.payment_data");
        }

        var update = new AgentCheckoutUpdate
        {
            Signals = AcpCheckoutMapper.ReadSignals(body),
            Consents = AcpCheckoutMapper.ReadConsents(body),
            Attribution = body["affiliate_attribution"]?.DeepClone() as JsonObject,
            OrderNotes = AgentJson.String(body, "order_notes"),
        };

        if (body["buyer"] is JsonObject)
        {
            update.Buyer = AcpCheckoutMapper.ReadInput(new JsonObject { ["buyer"] = body["buyer"].DeepClone() }).Buyer;
        }

        var result = await _checkouts.CompleteAsync(request.Context, id, payment, update, cancellationToken);

        return result.Succeeded
            ? AcpOperationResult.Success(await _mapper.WriteAsync(request, result, cancellationToken))
            : Fail(result);
    }

    /// <summary>
    /// Cancels a checkout session.
    /// </summary>
    /// <param name="request">The signed-in request.</param>
    /// <param name="id">The checkout session's id.</param>
    /// <param name="payload">A <c>CancelSessionRequest</c>, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<AcpOperationResult> CancelAsync(AgentRequest request, string id, JsonNode payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trace = (payload as JsonObject)?["intent_trace"]?.DeepClone() as JsonObject;
        var result = await _checkouts.CancelAsync(request.Context, id, trace, cancellationToken);

        if (result.Failure == AgentCheckoutFailure.InvalidState)
        {
            return AcpOperationResult.Error(StatusCodes.Status405MethodNotAllowed, "invalid_state", result.Error);
        }

        return result.Succeeded
            ? AcpOperationResult.Success(await _mapper.WriteAsync(request, result, cancellationToken))
            : Fail(result);
    }

    internal static AcpOperationResult Fail(AgentCheckoutResult result)
        => result.Failure switch
        {
            AgentCheckoutFailure.NotFound => AcpOperationResult.Error(StatusCodes.Status404NotFound, "not_found", result.Error),
            AgentCheckoutFailure.Busy => AcpOperationResult.Error(StatusCodes.Status409Conflict, "conflict", result.Error, retryAfterSeconds: 1),
            AgentCheckoutFailure.TooMany => AcpOperationResult.Error(StatusCodes.Status429TooManyRequests, "rate_limited", result.Error),
            AgentCheckoutFailure.InvalidState => AcpOperationResult.Error(StatusCodes.Status409Conflict, "invalid_state", result.Error),
            _ => AcpOperationResult.Error(StatusCodes.Status400BadRequest, "invalid", result.Error),
        };
}
