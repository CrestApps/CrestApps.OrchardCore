using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Orders.Api;

/// <summary>
/// The store's orders over the API, for staff and back-office systems with Manage orders: listing and finding them,
/// refunding and canceling, through the same operations as the admin's order page.
/// </summary>
internal static class OrderAdminApiEndpoints
{
    public const string Tag = "Orders";

    public static IEndpointRouteBuilder MapOrderAdminApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("orders", ListAsync)
            .WithName("ListOrders")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the store's orders, newest first: by status, customer, channel (online, pos, draft), the location they're sold from, date, or a search of number, name and email.");

        group.MapGet("orders/{orderId}", GetAsync)
            .WithName("GetOrder")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets an order, with what can still be refunded and what's still owed.");

        group.MapGet("orders/by-number/{number}", GetByNumberAsync)
            .WithName("GetOrderByNumber")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets an order by the number the buyer was given.");

        group.MapPost("orders/{orderId}/refunds", RefundAsync)
            .WithIdempotency()
            .WithName("RefundOrder")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Refunds an order, all of what's refundable or an amount: to how it was paid, or as store credit; optionally putting its stock back.");

        group.MapPost("orders/{orderId}/cancel", CancelAsync)
            .WithIdempotency()
            .WithName("CancelOrder")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Cancels an order, optionally refunding what was paid and putting its stock back.");

        return routes;
    }

    private static async Task<Results<Ok<ApiPage<ApiOrderSummary>>, ProblemHttpResult>> ListAsync(
        string status,
        string q,
        string customerId,
        string channel,
        string locationId,
        DateTime? from,
        DateTime? to,
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        OrderOperationsService operations)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, OrderPermissions.ManageOrders))
        {
            return ApiProblems.Forbidden();
        }

        var statuses = (status ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.TryParse<OrderStatus>(value, ignoreCase: true, out var parsed) ? parsed : (OrderStatus?)null)
            .Where(value => value.HasValue)
            .Select(value => value.Value)
            .ToArray();
        var pageSkip = ApiPage<ApiOrderSummary>.ClampSkip(skip);
        var pageTake = ApiPage<ApiOrderSummary>.ClampTake(take);
        var (count, page) = await orders.PageAsync(new OrderQuery
        {
            Statuses = statuses,
            CustomerId = string.IsNullOrWhiteSpace(customerId) ? null : customerId,
            Channel = string.IsNullOrWhiteSpace(channel) ? null : channel.Trim().ToLowerInvariant(),
            LocationId = string.IsNullOrWhiteSpace(locationId) ? null : locationId.Trim(),
            Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            CreatedOnOrAfter = from?.ToUniversalTime(),
            CreatedBefore = to?.ToUniversalTime(),
        }, pageSkip, pageTake, httpContext.RequestAborted);
        var items = new List<ApiOrderSummary>();

        foreach (var order in page)
        {
            items.Add(OrderApiMapper.ToSummary(order, await operations.IsOwedAsync(order, httpContext.RequestAborted)));
        }

        return TypedResults.Ok(new ApiPage<ApiOrderSummary>(items, count, pageSkip, pageTake));
    }

    private static async Task<Results<Ok<ApiStoreOrder>, ProblemHttpResult>> GetAsync(
        string orderId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        OrderOperationsService operations)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, OrderPermissions.ManageOrders))
        {
            return ApiProblems.Forbidden();
        }

        return await orders.FindByIdAsync(orderId, httpContext.RequestAborted) is { } order
            ? TypedResults.Ok(await ReadAsync(order, operations, httpContext))
            : ApiProblems.NotFound();
    }

    private static async Task<Results<Ok<ApiStoreOrder>, ProblemHttpResult>> GetByNumberAsync(
        string number,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        OrderOperationsService operations)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, OrderPermissions.ManageOrders))
        {
            return ApiProblems.Forbidden();
        }

        return await orders.FindByNumberAsync(number, httpContext.RequestAborted) is { } order
            ? TypedResults.Ok(await ReadAsync(order, operations, httpContext))
            : ApiProblems.NotFound();
    }

    private static async Task<Results<Ok<ApiOrderOperation>, ProblemHttpResult>> RefundAsync(
        string orderId,
        ApiRefundRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        OrderOperationsService operations)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, OrderPermissions.ManageOrders))
        {
            return ApiProblems.Forbidden();
        }

        if (await orders.FindByIdAsync(orderId, httpContext.RequestAborted) is not { } order)
        {
            return ApiProblems.NotFound();
        }

        var result = await operations.RefundAsync(order, request?.Amount, request?.Reason, request?.Restock == true, GetActorName(httpContext), request?.AsStoreCredit == true, httpContext.RequestAborted);

        return await RespondAsync(result, order, operations, httpContext);
    }

    private static async Task<Results<Ok<ApiOrderOperation>, ProblemHttpResult>> CancelAsync(
        string orderId,
        ApiCancelRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        OrderOperationsService operations)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, OrderPermissions.ManageOrders))
        {
            return ApiProblems.Forbidden();
        }

        if (await orders.FindByIdAsync(orderId, httpContext.RequestAborted) is not { } order)
        {
            return ApiProblems.NotFound();
        }

        var result = await operations.CancelAsync(order, request?.Note, request?.Refund == true, request?.Restock == true, GetActorName(httpContext), httpContext.RequestAborted);

        return await RespondAsync(result, order, operations, httpContext);
    }

    private static async Task<Results<Ok<ApiOrderOperation>, ProblemHttpResult>> RespondAsync(OrderOperationResult result, Order order, OrderOperationsService operations, HttpContext httpContext)
    {
        if (!result.Succeeded)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, string.Join(" ", result.Errors));
        }

        return TypedResults.Ok(new ApiOrderOperation(ApiMoney.From(result.Refunded, order.Currency), result.Messages, await ReadAsync(order, operations, httpContext)));
    }

    private static async Task<ApiStoreOrder> ReadAsync(Order order, OrderOperationsService operations, HttpContext httpContext)
    {
        var money = await operations.GetMoneyAsync(order, httpContext.RequestAborted);
        var isOwed = await operations.IsOwedAsync(order, httpContext.RequestAborted);

        return new ApiStoreOrder(
            OrderApiMapper.ToApi(order, isOwed, payUrl: null),
            ApiMoney.From(money.Refundable, money.Currency ?? order.Currency),
            ApiMoney.From(money.Outstanding, money.Currency ?? order.Currency),
            money.CanRefundAsStoreCredit,
            order.Notes.Select(note => new ApiOrderNote(note.Text, OrderApiMapper.ToCamel(note.Kind.ToString()), note.AuthorName, note.CreatedUtc)).ToList());
    }

    // Who did it, for the order's history: the signed-in user, or the client application a back-office system signs
    // in as.
    private static string GetActorName(HttpContext httpContext)
        => httpContext.User.Identity?.Name ??
            httpContext.User.FindFirst("name")?.Value ??
            httpContext.User.FindFirst("client_id")?.Value ??
            httpContext.User.FindFirst("azp")?.Value ??
            "API";
}

/// <summary>
/// An order as staff see it: the order, what can still be refunded and is still owed, and its history.
/// </summary>
public sealed record ApiStoreOrder(ApiOrder Order, ApiMoney Refundable, ApiMoney Outstanding, bool CanRefundAsStoreCredit, IReadOnlyList<ApiOrderNote> Notes);

/// <summary>
/// A note in an order's history.
/// </summary>
public sealed record ApiOrderNote(string Text, string Kind, string Author, DateTime CreatedUtc);

/// <summary>
/// What a refund or cancellation did: the money given back, what happened, and the order now.
/// </summary>
public sealed record ApiOrderOperation(ApiMoney Refunded, IReadOnlyList<string> Messages, ApiStoreOrder Order);

/// <summary>
/// A refund.
/// </summary>
public sealed class ApiRefundRequest
{
    /// <summary>
    /// How much to give back; everything refundable when not given.
    /// </summary>
    public decimal? Amount { get; set; }

    public string Reason { get; set; }

    /// <summary>
    /// Whether the order's stock goes back on the shelf.
    /// </summary>
    public bool? Restock { get; set; }

    /// <summary>
    /// Whether it's given as store credit instead of back to how it was paid.
    /// </summary>
    public bool? AsStoreCredit { get; set; }
}

/// <summary>
/// A cancellation.
/// </summary>
public sealed class ApiCancelRequest
{
    public string Note { get; set; }

    /// <summary>
    /// Whether what was paid is refunded.
    /// </summary>
    public bool? Refund { get; set; }

    /// <summary>
    /// Whether the order's stock goes back on the shelf.
    /// </summary>
    public bool? Restock { get; set; }
}
