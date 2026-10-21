using System.Security.Claims;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Inventory.Api;

/// <summary>
/// The store's stock over the API, for staff and back-office systems: listing it with View inventory, and receiving,
/// adjusting, counting and moving it with Manage inventory, through the same service as the admin's stock pages.
/// </summary>
internal static class InventoryAdminApiEndpoints
{
    public const string Tag = "Stock";

    public static IEndpointRouteBuilder MapInventoryAdminApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("inventory", ListAsync)
            .WithName("ListStock")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the stock the store counts: searched by SKU or name, or only what's running low.");

        group.MapGet("inventory/{stockItemId}", GetAsync)
            .WithName("GetStock")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets one stock item with what's on hand, held and available at each location.");

        group.MapPost("inventory/{stockItemId}/receive", ReceiveAsync)
            .WithIdempotency()
            .WithName("ReceiveStock")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Adds units received at a location.");

        group.MapPost("inventory/{stockItemId}/adjust", AdjustAsync)
            .WithIdempotency()
            .WithName("AdjustStock")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Changes what's on hand at a location by a number of units, up or down, with the reason.");

        group.MapPut("inventory/{stockItemId}/count", CountAsync)
            .WithName("CountStock")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Sets what's on hand at a location to what was counted.");

        group.MapPost("inventory/{stockItemId}/transfer", TransferAsync)
            .WithIdempotency()
            .WithName("TransferStock")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Moves units from one location to another.");

        return routes;
    }

    private static async Task<Results<Ok<ApiPage<ApiStockItem>>, ProblemHttpResult>> ListAsync(
        string q,
        bool? low,
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IStockItemStore stockItems)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InventoryPermissions.ViewInventory))
        {
            return ApiProblems.Forbidden();
        }

        var pageSkip = ApiPage<ApiStockItem>.ClampSkip(skip);
        var pageTake = ApiPage<ApiStockItem>.ClampTake(take);
        var (count, items) = await stockItems.PageAsync(new StockItemQuery
        {
            Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            Filter = low == true ? StockItemFilter.Low : StockItemFilter.All,
        }, pageSkip, pageTake, httpContext.RequestAborted);

        return TypedResults.Ok(new ApiPage<ApiStockItem>(items.Select(StockItemApiMapper.ToApi).ToList(), count, pageSkip, pageTake));
    }

    private static async Task<Results<Ok<ApiStockItem>, ProblemHttpResult>> GetAsync(
        string stockItemId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IStockItemStore stockItems)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InventoryPermissions.ViewInventory))
        {
            return ApiProblems.Forbidden();
        }

        return await stockItems.FindByIdAsync(stockItemId, httpContext.RequestAborted) is { } item
            ? TypedResults.Ok(StockItemApiMapper.ToApi(item))
            : ApiProblems.NotFound();
    }

    private static Task<Results<Ok<ApiStockItem>, ValidationProblem, ProblemHttpResult>> ReceiveAsync(string stockItemId, ApiStockChangeRequest request, HttpContext httpContext)
        => ChangeAsync(
            httpContext,
            request?.Quantity is > 0 ? null : ("quantity", "Receive at least one."),
            [("locationId", request?.LocationId)],
            (inventory, location, actor) => inventory.AdjustAsync(stockItemId, location(request.LocationId), StockMovementKind.Received, request.Quantity.Value, request.Reason, actor, httpContext.RequestAborted));

    private static Task<Results<Ok<ApiStockItem>, ValidationProblem, ProblemHttpResult>> AdjustAsync(string stockItemId, ApiStockChangeRequest request, HttpContext httpContext)
        => ChangeAsync(
            httpContext,
            request?.Quantity is { } quantity && quantity != 0 ? null : ("quantity", "Give the change in units, up or down."),
            [("locationId", request?.LocationId)],
            (inventory, location, actor) => inventory.AdjustAsync(stockItemId, location(request.LocationId), StockMovementKind.Adjusted, request.Quantity.Value, request.Reason, actor, httpContext.RequestAborted));

    private static Task<Results<Ok<ApiStockItem>, ValidationProblem, ProblemHttpResult>> CountAsync(string stockItemId, ApiStockCountRequest request, HttpContext httpContext)
        => ChangeAsync(
            httpContext,
            request?.Count is >= 0 ? null : ("count", "Give what was counted, zero or more."),
            [("locationId", request?.LocationId)],
            (inventory, location, actor) => inventory.SetCountAsync(stockItemId, location(request.LocationId), request.Count.Value, request.Reason, actor, httpContext.RequestAborted));

    private static Task<Results<Ok<ApiStockItem>, ValidationProblem, ProblemHttpResult>> TransferAsync(string stockItemId, ApiStockTransferRequest request, HttpContext httpContext)
        => ChangeAsync(
            httpContext,
            request?.Quantity is not > 0
                ? ("quantity", "Move at least one.")
                : string.IsNullOrEmpty(request.FromLocationId) || string.IsNullOrEmpty(request.ToLocationId) || request.FromLocationId == request.ToLocationId
                    ? ("toLocationId", "Name two different locations.")
                    : null,
            [("fromLocationId", request?.FromLocationId), ("toLocationId", request?.ToLocationId)],
            (inventory, location, actor) => inventory.TransferAsync(stockItemId, request.FromLocationId, request.ToLocationId, request.Quantity.Value, request.Reason, actor, httpContext.RequestAborted));

    // Every change is committed here, so a checkout that changed the same stock at the same moment can't be overwritten:
    // the later of the two fails, and the caller is told to try again.
    private static async Task<Results<Ok<ApiStockItem>, ValidationProblem, ProblemHttpResult>> ChangeAsync(
        HttpContext httpContext,
        (string Field, string Error)? invalid,
        IReadOnlyList<(string Field, string LocationId)> locations,
        Func<IInventoryService, Func<string, string>, StockActor, Task<StockChangeResult>> change)
    {
        var services = httpContext.RequestServices;

        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, InventoryPermissions.ManageInventory))
        {
            return ApiProblems.Forbidden();
        }

        if (invalid is { } problem)
        {
            return ApiProblems.Validation(problem.Field, problem.Error);
        }

        var known = await services.GetRequiredService<IInventoryLocationService>().GetLocationIdsAsync();

        foreach (var (field, locationId) in locations)
        {
            if (!string.IsNullOrEmpty(locationId) && !known.Contains(locationId))
            {
                return ApiProblems.Validation(field, "There's no location with that id.");
            }
        }

        var actor = StockActor.ForUser(httpContext.User, httpContext.User.FindFirstValue("client_id") ?? "API");

        var result = await change(
            services.GetRequiredService<IInventoryService>(),
            locationId => InventoryLocationServiceExtensions.KnownOrDefault(known, locationId),
            actor);

        if (!result.Succeeded)
        {
            return result.Error switch
            {
                StockChangeError.NotFound => ApiProblems.NotFound(),
                StockChangeError.Busy => ApiProblems.Conflict(ApiProblems.Codes.Concurrency, "The stock is being changed elsewhere right now. Try again."),
                StockChangeError.NotEnoughAtLocation => ApiProblems.Conflict(ApiProblems.Codes.OutOfStock, "There aren't that many at the location."),
                _ => ApiProblems.Validation("quantity", "That change isn't possible."),
            };
        }

        try
        {
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(httpContext.RequestAborted);
        }
        catch (ConcurrencyException)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Concurrency, "The stock was changed elsewhere at the same time. Read it again and retry.");
        }

        return TypedResults.Ok(StockItemApiMapper.ToApi(result.Item));
    }
}

/// <summary>
/// A product or variant the store counts, and its stock at each location.
/// </summary>
public sealed record ApiStockItem(
    string Id,
    string ProductId,
    string VariantId,
    string Sku,
    string Title,
    int OnHand,
    int Reserved,
    int Available,
    int LowStockThreshold,
    bool AllowBackorder,
    bool Archived,
    IReadOnlyList<ApiStockLevel> Levels);

/// <summary>
/// The stock at one location: on hand, held for orders, and available to sell.
/// </summary>
public sealed record ApiStockLevel(string LocationId, int OnHand, int Reserved, int Available);

/// <summary>
/// A number of units received, or a change up or down.
/// </summary>
public sealed class ApiStockChangeRequest
{
    public string LocationId { get; set; }

    public int? Quantity { get; set; }

    public string Reason { get; set; }
}

/// <summary>
/// What was counted at a location.
/// </summary>
public sealed class ApiStockCountRequest
{
    public string LocationId { get; set; }

    public int? Count { get; set; }

    public string Reason { get; set; }
}

/// <summary>
/// Units moved between locations.
/// </summary>
public sealed class ApiStockTransferRequest
{
    public string FromLocationId { get; set; }

    public string ToLocationId { get; set; }

    public int? Quantity { get; set; }

    public string Reason { get; set; }
}
