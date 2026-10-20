using System.Security.Claims;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Api;

/// <summary>
/// The signed-in buyer's orders and returns over the API, as the storefront's account pages show them: the list has
/// their placed orders; one of them, or a draft the store sent them to pay, opens by its id.
/// </summary>
internal static class OrderAccountApiEndpoints
{
    public const string Tag = "Account";

    public static IEndpointRouteBuilder MapOrderAccountApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("me/orders", ListAsync)
            .WithName("ListMyOrders")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the signed-in buyer's orders, newest first.");

        group.MapGet("me/orders/{orderId}", GetAsync)
            .WithName("GetMyOrder")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets one of the signed-in buyer's orders.");

        group.MapPost("me/quotes", RequestQuoteAsync)
            .WithIdempotency()
            .WithName("RequestQuote")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Asks for a quote for the buyer's cart: it becomes a draft order for the store to price and send, and the cart is emptied.");

        group.MapPost("me/orders/{orderId}/decline-quote", DeclineQuoteAsync)
            .WithName("DeclineMyQuote")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Turns down a quote, or withdraws the request: the order is canceled.");

        return routes;
    }

    public static IEndpointRouteBuilder MapOrderReturnsAccountApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("me/orders/{orderId}/returns", ListReturnsAsync)
            .WithName("ListMyOrderReturns")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists the returns asked for on one of the buyer's orders.");

        group.MapGet("me/orders/{orderId}/returnable", GetReturnableAsync)
            .WithName("GetMyOrderReturnable")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets what can still be returned from a paid order, and the reasons to choose from.");

        group.MapPost("me/orders/{orderId}/returns", RequestReturnAsync)
            .WithIdempotency()
            .WithName("RequestMyOrderReturn")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Asks for a return of some of a paid order's lines, for a refund or an exchange.");

        group.MapPost("me/orders/{orderId}/returns/{returnId}/withdraw", WithdrawReturnAsync)
            .WithName("WithdrawMyOrderReturn")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Withdraws a return the store hasn't received yet.");

        return routes;
    }

    private static async Task<Results<Ok<ApiPage<ApiOrderSummary>>, ProblemHttpResult>> ListAsync(
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        OrderOperationsService operations)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, OrderPermissions.ViewOwnOrders))
        {
            return ApiProblems.Forbidden();
        }

        var pageSkip = ApiPage<ApiOrderSummary>.ClampSkip(skip);
        var pageTake = ApiPage<ApiOrderSummary>.ClampTake(take);
        var (count, page) = await orders.PageAsync(new OrderQuery
        {
            UserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
            Statuses = [OrderStatus.AwaitingPayment, OrderStatus.Paid, OrderStatus.Canceled],
        }, pageSkip, pageTake, httpContext.RequestAborted);
        var items = new List<ApiOrderSummary>();

        foreach (var order in page)
        {
            items.Add(OrderApiMapper.ToSummary(order, await operations.IsOwedAsync(order, httpContext.RequestAborted)));
        }

        return TypedResults.Ok(new ApiPage<ApiOrderSummary>(items, count, pageSkip, pageTake));
    }

    private static async Task<Results<Ok<ApiOrder>, ProblemHttpResult>> GetAsync(
        string orderId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        OrderOperationsService operations)
    {
        var order = await FindOwnAsync(orderId, httpContext, authorizationService, orders, paidOnly: false);

        if (order is null)
        {
            return ApiProblems.NotFound();
        }

        var payUrl = order.Status == OrderStatus.Canceled || !QuoteService.CanPay(order, DateTime.UtcNow) ? null : await OrderPayLinks.GetPayUrlAsync(httpContext.RequestServices, order);

        return TypedResults.Ok(OrderApiMapper.ToApi(order, await operations.IsOwedAsync(order, httpContext.RequestAborted), payUrl));
    }

    private static async Task<Results<Created<ApiOrder>, ValidationProblem, ProblemHttpResult>> RequestQuoteAsync(
        ApiQuoteRequest request,
        HttpContext httpContext,
        QuoteService quotes)
    {
        if (!await quotes.CanRequestAsync(httpContext.User))
        {
            return ApiProblems.NotFound("The store doesn't take quote requests.");
        }

        var (quote, problems) = await quotes.RequestAsync(httpContext.User, request?.Message, request?.PurchaseOrderNumber, httpContext.RequestAborted);

        if (quote is null)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { ["cart"] = [.. problems] });
        }

        return TypedResults.Created($"{httpContext.Request.PathBase}/{CommerceApi.RoutePrefix}/me/orders/{quote.ItemId}", OrderApiMapper.ToApi(quote, isOwed: false, payUrl: null));
    }

    private static async Task<Results<Ok<ApiOrder>, ProblemHttpResult>> DeclineQuoteAsync(
        string orderId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        QuoteService quotes)
    {
        if (await FindOwnAsync(orderId, httpContext, authorizationService, orders, paidOnly: false) is not { Quote: not null } order)
        {
            return ApiProblems.NotFound();
        }

        if (!await quotes.DeclineAsync(order, httpContext.User.Identity?.Name, httpContext.RequestAborted))
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "The quote is paid, being paid, or already turned down.");
        }

        return TypedResults.Ok(OrderApiMapper.ToApi(order, isOwed: false, payUrl: null));
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiOrderReturn>>, ProblemHttpResult>> ListReturnsAsync(
        string orderId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        IOrderReturnStore returns)
    {
        if (await FindOwnAsync(orderId, httpContext, authorizationService, orders, paidOnly: false) is not { } order)
        {
            return ApiProblems.NotFound();
        }

        var list = await returns.ListByOrderAsync(order.ItemId, httpContext.RequestAborted);

        return TypedResults.Ok<IReadOnlyList<ApiOrderReturn>>(list.Select(OrderApiMapper.ToApi).ToList());
    }

    private static async Task<Results<Ok<ApiReturnable>, ProblemHttpResult>> GetReturnableAsync(
        string orderId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        OrderReturnService returnService,
        ISiteService siteService)
    {
        if (await FindOwnAsync(orderId, httpContext, authorizationService, orders, paidOnly: true) is not { } order)
        {
            return ApiProblems.NotFound();
        }

        var returnable = await returnService.GetReturnableAsync(order, httpContext.RequestAborted);
        var settings = await siteService.GetSettingsAsync<ReturnSettings>();

        return TypedResults.Ok(new ApiReturnable(
            order.Lines
                .Where(line => returnable.ContainsKey(line.LineId))
                .Select(line => new ApiReturnableLine(line.LineId, OrderLineText.Describe(line), line.Sku, returnable[line.LineId]))
                .ToList(),
            settings.GetReasons(),
            settings.AllowExchanges,
            settings.WindowDays));
    }

    private static async Task<Results<Created<ApiOrderReturn>, ValidationProblem, ProblemHttpResult>> RequestReturnAsync(
        string orderId,
        ApiReturnRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        OrderReturnService returnService)
    {
        if (await FindOwnAsync(orderId, httpContext, authorizationService, orders, paidOnly: true) is not { } order)
        {
            return ApiProblems.NotFound();
        }

        var (orderReturn, problems) = await returnService.RequestAsync(order, new OrderReturnRequest
        {
            Lines = (request?.Lines ?? [])
                .Where(line => !string.IsNullOrEmpty(line?.LineId))
                .Select(line => (line.LineId, line.Quantity, line.Reason))
                .ToArray(),
            Resolution = string.Equals(request?.Resolution, "exchange", StringComparison.OrdinalIgnoreCase) ? OrderReturnResolution.Exchange : OrderReturnResolution.Refund,
            ExchangeFor = request?.ExchangeFor,
            Comment = request?.Comment,
        }, httpContext.RequestAborted);

        if (problems.Count > 0 || orderReturn is null)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { ["lines"] = [.. problems] });
        }

        return TypedResults.Created($"{httpContext.Request.PathBase}/{CommerceApi.RoutePrefix}/me/orders/{order.ItemId}/returns", OrderApiMapper.ToApi(orderReturn));
    }

    private static async Task<Results<Ok<ApiOrderReturn>, ProblemHttpResult>> WithdrawReturnAsync(
        string orderId,
        string returnId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        IOrderReturnStore returns,
        OrderReturnService returnService)
    {
        if (await FindOwnAsync(orderId, httpContext, authorizationService, orders, paidOnly: true) is not { } order ||
            await returns.FindByIdAsync(returnId, httpContext.RequestAborted) is not { } orderReturn ||
            !string.Equals(orderReturn.OrderId, order.ItemId, StringComparison.Ordinal))
        {
            return ApiProblems.NotFound();
        }

        return await returnService.WithdrawAsync(orderReturn, httpContext.RequestAborted)
            ? TypedResults.Ok(OrderApiMapper.ToApi(orderReturn))
            : ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, "This return can't be withdrawn any more.");
    }

    // Only the buyer's own placed orders, and drafts sent to them to pay; anything else reads as not found.
    private static async Task<Order> FindOwnAsync(string orderId, HttpContext httpContext, IAuthorizationService authorizationService, IOrderStore orders, bool paidOnly)
    {
        var order = string.IsNullOrEmpty(orderId) ? null : await orders.FindByIdAsync(orderId, httpContext.RequestAborted);

        return order is null ||
            (order.Status == OrderStatus.Draft && order.Invoice?.SentUtc is null && order.Quote is null) ||
            (paidOnly && order.Status != OrderStatus.Paid) ||
            !string.Equals(order.UserId, httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal) ||
            !await authorizationService.AuthorizeAsync(httpContext.User, OrderPermissions.ViewOwnOrders)
            ? null
            : order;
    }
}
