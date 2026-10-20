using System.Security.Claims;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Shipping.Api;

/// <summary>
/// Shipments over the API: the buyer tracks theirs, and staff with Fulfill orders ship orders.
/// </summary>
internal static class ShipmentApiEndpoints
{
    // The Orders module's permission, by name: what lets a buyer see their own orders.
    private static readonly Permission _viewOwnOrders = new("ViewOwnOrders", "View own orders");

    public static IEndpointRouteBuilder MapShipmentApi(this IEndpointRouteBuilder routes)
    {
        var account = routes.MapCommerceApi("Account");

        account.MapGet("me/orders/{orderId}/shipments", ListMineAsync)
            .WithName("ListMyOrderShipments")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists the shipments of one of the buyer's orders, with their tracking.");

        var admin = routes.MapCommerceApi("Fulfillment");

        admin.MapGet("orders/{orderId}/shipments", ListAsync)
            .WithName("ListOrderShipments")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists an order's shipments, and how many of each line are still to ship.");

        admin.MapPost("orders/{orderId}/shipments", CreateAsync)
            .WithIdempotency()
            .WithName("CreateOrderShipment")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Packs lines of a paid order into a shipment, shipped now with its tracking or kept to ship later. No lines packs all that's left.");

        admin.MapPost("shipments/{shipmentId}/ship", ShipAsync)
            .WithName("ShipShipment")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Marks a shipment shipped, with its carrier and tracking number; the buyer is emailed.");

        admin.MapPost("shipments/{shipmentId}/deliver", DeliverAsync)
            .WithName("DeliverShipment")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Marks a shipment delivered.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiShipment>>, ProblemHttpResult>> ListMineAsync(
        string orderId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        IShipmentStore shipments)
    {
        var order = string.IsNullOrEmpty(orderId) ? null : await orders.FindByIdAsync(orderId, httpContext.RequestAborted);

        if (order is null ||
            order.Status != OrderStatus.Paid ||
            !string.Equals(order.UserId, httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal) ||
            !await authorizationService.AuthorizeAsync(httpContext.User, _viewOwnOrders))
        {
            return ApiProblems.NotFound();
        }

        var list = await shipments.ListByOrderAsync(order.ItemId, httpContext.RequestAborted);

        return TypedResults.Ok<IReadOnlyList<ApiShipment>>(list.Select(ToApi).ToList());
    }

    private static async Task<Results<Ok<ApiOrderFulfillment>, ProblemHttpResult>> ListAsync(
        string orderId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        IShipmentStore shipments,
        IShipmentService shipmentService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ShippingPermissions.FulfillOrders))
        {
            return ApiProblems.Forbidden();
        }

        if (await orders.FindByIdAsync(orderId, httpContext.RequestAborted) is not { } order)
        {
            return ApiProblems.NotFound();
        }

        var remaining = await shipmentService.GetRemainingAsync(order, httpContext.RequestAborted);
        var list = await shipments.ListByOrderAsync(order.ItemId, httpContext.RequestAborted);

        return TypedResults.Ok(new ApiOrderFulfillment(
            order.Lines
                .Where(line => remaining.TryGetValue(line.LineId, out var count) && count > 0)
                .Select(line => new ApiShipmentLine(line.LineId, line.Title, line.Sku, remaining[line.LineId]))
                .ToList(),
            list.Select(ToApi).ToList()));
    }

    private static async Task<Results<Created<ApiShipment>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        string orderId,
        ApiCreateShipmentRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOrderStore orders,
        IShipmentService shipmentService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ShippingPermissions.FulfillOrders))
        {
            return ApiProblems.Forbidden();
        }

        if (await orders.FindByIdAsync(orderId, httpContext.RequestAborted) is not { } order)
        {
            return ApiProblems.NotFound();
        }

        IEnumerable<ShipmentLineRequest> lines = request?.Lines is { Count: > 0 } chosen
            ? chosen.Where(line => !string.IsNullOrEmpty(line?.LineId)).Select(line => new ShipmentLineRequest(line.LineId, line.Quantity))
            : (await shipmentService.GetRemainingAsync(order, httpContext.RequestAborted)).Where(pair => pair.Value > 0).Select(pair => new ShipmentLineRequest(pair.Key, pair.Value));

        var result = await shipmentService.CreateAsync(
            order,
            lines.ToList(),
            new ShipmentDetails(request?.Carrier, request?.TrackingNumber, request?.TrackingUrl, request?.Note),
            ship: request?.Ship != false,
            httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, Describe(result.Error));
        }

        return TypedResults.Created($"{httpContext.Request.PathBase}/{CommerceApi.RoutePrefix}/orders/{order.ItemId}/shipments", ToApi(result.Shipment));
    }

    private static async Task<Results<Ok<ApiShipment>, ProblemHttpResult>> ShipAsync(
        string shipmentId,
        ApiShipRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IShipmentStore shipments,
        IShipmentService shipmentService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ShippingPermissions.FulfillOrders))
        {
            return ApiProblems.Forbidden();
        }

        if (await shipments.FindByIdAsync(shipmentId, httpContext.RequestAborted) is not { } shipment)
        {
            return ApiProblems.NotFound();
        }

        var result = await shipmentService.ShipAsync(shipment, new ShipmentDetails(request?.Carrier, request?.TrackingNumber, request?.TrackingUrl, request?.Note), httpContext.RequestAborted);

        return result.Succeeded
            ? TypedResults.Ok(ToApi(result.Shipment ?? shipment))
            : ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, Describe(result.Error));
    }

    private static async Task<Results<Ok<ApiShipment>, ProblemHttpResult>> DeliverAsync(
        string shipmentId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IShipmentStore shipments,
        IShipmentService shipmentService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ShippingPermissions.FulfillOrders))
        {
            return ApiProblems.Forbidden();
        }

        if (await shipments.FindByIdAsync(shipmentId, httpContext.RequestAborted) is not { } shipment)
        {
            return ApiProblems.NotFound();
        }

        var result = await shipmentService.DeliverAsync(shipment, httpContext.RequestAborted);

        return result.Succeeded
            ? TypedResults.Ok(ToApi(result.Shipment ?? shipment))
            : ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, Describe(result.Error));
    }

    private static string Describe(ShipmentError error)
        => error switch
        {
            ShipmentError.OrderNotPaid => "Only a paid order is shipped.",
            ShipmentError.NothingToShip => "The shipment would hold nothing.",
            ShipmentError.TooMany => "That's more than the order has left to ship.",
            ShipmentError.InvalidStatus => "The shipment can't change that way from where it is.",
            _ => "The shipment couldn't be changed.",
        };

    internal static ApiShipment ToApi(Shipment shipment)
        => new(
            shipment.ItemId,
            shipment.OrderId,
            shipment.Number,
            char.ToLowerInvariant(shipment.Status.ToString()[0]) + shipment.Status.ToString()[1..],
            shipment.Carrier,
            shipment.TrackingNumber,
            shipment.TrackingUrl,
            shipment.TrackingStatus,
            shipment.Lines.Select(line => new ApiShipmentLine(line.LineId, line.Title, line.Sku, line.Quantity)).ToList(),
            (shipment.TrackingEvents ?? []).Select(entry => new ApiShipmentTrackingEvent(entry.Utc, entry.Status, entry.Message, entry.Location)).ToList(),
            shipment.ShippedUtc,
            shipment.DeliveredUtc);
}

/// <summary>
/// A shipment: lines of an order sent together, and where they are.
/// </summary>
/// <param name="Status"><c>pending</c>, <c>shipped</c>, <c>delivered</c> or <c>returned</c>.</param>
public sealed record ApiShipment(
    string Id,
    string OrderId,
    int Number,
    string Status,
    string Carrier,
    string TrackingNumber,
    string TrackingUrl,
    string TrackingStatus,
    IReadOnlyList<ApiShipmentLine> Lines,
    IReadOnlyList<ApiShipmentTrackingEvent> TrackingEvents,
    DateTime? ShippedUtc,
    DateTime? DeliveredUtc);

/// <summary>
/// A line in a shipment, or still to ship.
/// </summary>
public sealed record ApiShipmentLine(string LineId, string Title, string Sku, int Quantity);

/// <summary>
/// What the carrier said about a shipment.
/// </summary>
public sealed record ApiShipmentTrackingEvent(DateTime Utc, string Status, string Message, string Location);

/// <summary>
/// An order's fulfillment: what's still to ship, and the shipments so far.
/// </summary>
public sealed record ApiOrderFulfillment(IReadOnlyList<ApiShipmentLine> Remaining, IReadOnlyList<ApiShipment> Shipments);

/// <summary>
/// A shipment to pack.
/// </summary>
public sealed class ApiCreateShipmentRequest
{
    /// <summary>
    /// The lines and how many of each; none packs everything still to ship.
    /// </summary>
    public List<ApiShipmentLineRequest> Lines { get; set; } = [];

    /// <summary>
    /// Whether it's shipped now (the default) or kept to ship later.
    /// </summary>
    public bool? Ship { get; set; }

    public string Carrier { get; set; }

    public string TrackingNumber { get; set; }

    public string TrackingUrl { get; set; }

    public string Note { get; set; }
}

/// <summary>
/// A line to pack, and how many.
/// </summary>
public sealed class ApiShipmentLineRequest
{
    public string LineId { get; set; }

    public int Quantity { get; set; }
}

/// <summary>
/// The tracking a shipment ships with.
/// </summary>
public sealed class ApiShipRequest
{
    public string Carrier { get; set; }

    public string TrackingNumber { get; set; }

    public string TrackingUrl { get; set; }

    public string Note { get; set; }
}
