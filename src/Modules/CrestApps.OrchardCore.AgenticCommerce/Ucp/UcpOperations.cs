using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Orders;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// UCP's checkout and order operations, shared by its bindings: the REST endpoints and the A2A agent take the same
/// UCP JSON and get the same answers. Each operation takes a request that <see cref="UcpRequestFilter"/> signed in and
/// negotiated, and returns a UCP body or a transport error for the binding to write.
/// </summary>
public sealed class UcpOperations
{
    private readonly IAgentCheckoutService _checkouts;
    private readonly UcpCheckoutMapper _mapper;
    private readonly IEnumerable<IUcpCheckoutExtension> _extensions;
    private readonly IAgentCheckoutStore _store;
    private readonly IOrderStore _orders;
    private readonly AgentOrderViewBuilder _orderViews;

    /// <summary>
    /// Initializes a new instance of the <see cref="UcpOperations"/> class.
    /// </summary>
    /// <param name="checkouts">The agent checkout.</param>
    /// <param name="mapper">The UCP checkout mapper.</param>
    /// <param name="extensions">The checkout extensions.</param>
    /// <param name="store">The agent checkout store, for carts and orders.</param>
    /// <param name="orders">The order store.</param>
    /// <param name="orderViews">The order view builder.</param>
    public UcpOperations(
        IAgentCheckoutService checkouts,
        UcpCheckoutMapper mapper,
        IEnumerable<IUcpCheckoutExtension> extensions,
        IAgentCheckoutStore store,
        IOrderStore orders,
        AgentOrderViewBuilder orderViews)
    {
        _checkouts = checkouts;
        _mapper = mapper;
        _extensions = extensions;
        _store = store;
        _orders = orders;
        _orderViews = orderViews;
    }

    /// <summary>
    /// Creates a checkout session; answered with HTTP 201 by the REST binding.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="body">The checkout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<UcpOperationResult> CreateCheckoutAsync(AgentRequest request, JsonNode body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (body is not JsonObject checkout)
        {
            return UcpOperationResult.Fail(StatusCodes.Status400BadRequest, UcpOperationResult.InvalidRequestCode, "Send the checkout as a JSON object.");
        }

        var input = UcpCheckoutMapper.ReadInput(checkout);

        // A checkout may start from a cart the agent built: its items, unless the request gives others.
        if (input.Lines.Count == 0 &&
            AgentJson.String(checkout, "cart_id") is { } cartId &&
            await UcpCatalogEndpoints.FindCartAsync(_store, request, cartId, cancellationToken) is { } cart)
        {
            input.Lines = [.. cart.Lines];
        }

        var result = await _checkouts.CreateAsync(request.Context, input, cancellationToken);

        if (!result.Succeeded)
        {
            return await FailAsync(request, result, cancellationToken);
        }

        return UcpOperationResult.Answer(await _mapper.WriteAsync(request, result, cancellationToken), StatusCodes.Status201Created);
    }

    /// <summary>
    /// Returns a checkout session.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="id">The checkout's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<UcpOperationResult> GetCheckoutAsync(AgentRequest request, string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _checkouts.GetAsync(request.Context, id, cancellationToken);

        return result.Succeeded
            ? UcpOperationResult.Answer(await _mapper.WriteAsync(request, result, cancellationToken))
            : await FailAsync(request, result, cancellationToken);
    }

    /// <summary>
    /// Replaces a checkout session's line items, buyer, fulfillment and discount codes.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="id">The checkout's id.</param>
    /// <param name="body">The checkout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<UcpOperationResult> UpdateCheckoutAsync(AgentRequest request, string id, JsonNode body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (body is not JsonObject checkout)
        {
            return UcpOperationResult.Fail(StatusCodes.Status400BadRequest, UcpOperationResult.InvalidRequestCode, "Send the checkout as a JSON object.");
        }

        var result = await _checkouts.UpdateAsync(request.Context, id, UcpCheckoutMapper.ReadUpdate(checkout), cancellationToken);

        return result.Succeeded
            ? UcpOperationResult.Answer(await _mapper.WriteAsync(request, result, cancellationToken))
            : await FailAsync(request, result, cancellationToken);
    }

    /// <summary>
    /// Completes a checkout session with a payment instrument, after every checkout extension agreed, and places the
    /// order.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="id">The checkout's id.</param>
    /// <param name="body">The payment, with any other members the extensions read (such as AP2's mandate).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<UcpOperationResult> CompleteCheckoutAsync(AgentRequest request, string id, JsonNode body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (body is not JsonObject json)
        {
            return UcpOperationResult.Fail(StatusCodes.Status400BadRequest, UcpOperationResult.InvalidRequestCode, "Send the payment as a JSON object.");
        }

        if (UcpCheckoutMapper.ReadPayment(json) is not { } payment)
        {
            return UcpOperationResult.Answer(UcpCheckoutMapper.WriteError(request, "Give the payment instrument to complete the checkout with.", AgentMessageCodes.PaymentFailed));
        }

        var result = await CompleteAsync(request, id, payment, json, cancellationToken);

        return result.Succeeded
            ? UcpOperationResult.Answer(await _mapper.WriteAsync(request, result, cancellationToken))
            : await FailAsync(request, result, cancellationToken);
    }

    /// <summary>
    /// Completes a checkout session after every checkout extension agreed, and lets them keep what they need once it
    /// completed (such as AP2's mandate and receipt). Every binding (REST, MCP, A2A) completes through it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="id">The checkout's id.</param>
    /// <param name="payment">The payment.</param>
    /// <param name="json">The complete request's checkout JSON, with any other members the extensions read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completion; a refusal by an extension is <see cref="AgentCheckoutFailure.Refused"/>.</returns>
    public async Task<AgentCheckoutResult> CompleteAsync(AgentRequest request, string id, AgentPayment payment, JsonObject json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(json);

        var current = await _checkouts.GetAsync(request.Context, id, cancellationToken);

        if (!current.Succeeded)
        {
            return current;
        }

        var context = new UcpCheckoutContext
        {
            Request = request,
            Checkout = current.Checkout,
            View = current.View,
            Json = json,
        };

        foreach (var extension in _extensions)
        {
            if (await extension.CompletingAsync(context) is { } refusal)
            {
                current.View.Messages.Add(refusal);

                return AgentCheckoutResult.Fail(AgentCheckoutFailure.Refused, error: null, current.Checkout, current.View);
            }
        }

        var update = UcpCheckoutMapper.ReadUpdate(json);

        update.Lines = null;

        var result = await _checkouts.CompleteAsync(request.Context, id, payment, update, cancellationToken);

        if (result.Succeeded && result.View?.Status == AgentCheckoutStatus.Completed)
        {
            context.Checkout = result.Checkout;
            context.View = result.View;

            foreach (var extension in _extensions)
            {
                await extension.CompletedAsync(context);
            }
        }

        return result;
    }

    /// <summary>
    /// Cancels a checkout session.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="id">The checkout's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<UcpOperationResult> CancelCheckoutAsync(AgentRequest request, string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _checkouts.CancelAsync(request.Context, id, intentTrace: null, cancellationToken);

        return result.Succeeded
            ? UcpOperationResult.Answer(await _mapper.WriteAsync(request, result, cancellationToken))
            : await FailAsync(request, result, cancellationToken);
    }

    /// <summary>
    /// Returns an order the platform's agents placed or, with a linked account, one of the buyer's own orders.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="id">The order's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<UcpOperationResult> GetOrderAsync(AgentRequest request, string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = string.IsNullOrEmpty(id) ? null : await _orders.FindByIdAsync(id, cancellationToken);
        var checkout = order is null ? null : await _store.FindByOrderIdAsync(order.ItemId, cancellationToken);

        // A platform reads the orders its agents placed; with a linked account, the buyer's own orders too.
        var mine = checkout is not null && string.Equals(checkout.PlatformId, request.Platform?.ItemId, StringComparison.Ordinal);
        var buyers = order is not null &&
            !string.IsNullOrEmpty(request.LinkedUserId) &&
            request.Scopes.Contains(UcpIdentityScopes.OrderRead) &&
            string.Equals(order.UserId, request.LinkedUserId, StringComparison.Ordinal);

        if (order is null || (!mine && !buyers))
        {
            return UcpOperationResult.Fail(StatusCodes.Status404NotFound, UcpOperationResult.NotFoundCode, "There is no such order.");
        }

        return UcpOperationResult.Answer(UcpOrderMapper.Write(await _orderViews.BuildAsync(order, checkout, cancellationToken), request.Version));
    }

    private async Task<UcpOperationResult> FailAsync(AgentRequest request, AgentCheckoutResult result, CancellationToken cancellationToken)
    {
        switch (result.Failure)
        {
            case AgentCheckoutFailure.NotFound:
                return UcpOperationResult.Fail(StatusCodes.Status404NotFound, UcpOperationResult.NotFoundCode, result.Error);

            case AgentCheckoutFailure.Busy:
                return UcpOperationResult.Fail(StatusCodes.Status409Conflict, UcpOperationResult.ConflictCode, result.Error, retryAfter: 1);

            case AgentCheckoutFailure.TooMany:
                return UcpOperationResult.Fail(StatusCodes.Status429TooManyRequests, UcpOperationResult.RateLimitedCode, result.Error);
        }

        // Changing, completing or canceling a checkout that can't be any more (completed, canceled, expired) is refused
        // with 409, as the UCP conformance suite requires; the body is still the checkout, with the reason in messages.
        var statusCode = result.Failure == AgentCheckoutFailure.InvalidState ? StatusCodes.Status409Conflict : StatusCodes.Status200OK;

        if (result.View is not null && result.Checkout is not null)
        {
            return UcpOperationResult.Answer(await _mapper.WriteAsync(request, result, cancellationToken), statusCode);
        }

        if (statusCode == StatusCodes.Status409Conflict)
        {
            return UcpOperationResult.Answer(UcpCheckoutMapper.WriteError(request, result.Error, AgentMessageCodes.InvalidState), statusCode);
        }

        return UcpOperationResult.Answer(UcpCheckoutMapper.WriteError(request, result.Error));
    }
}
