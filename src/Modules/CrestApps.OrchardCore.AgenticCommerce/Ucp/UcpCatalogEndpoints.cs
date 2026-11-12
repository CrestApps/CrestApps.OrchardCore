using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// UCP's catalog search and lookup and the cart, on the REST binding.
/// </summary>
public static class UcpCatalogEndpoints
{
    /// <summary>
    /// The storage key that marks an agent checkout record as a UCP cart.
    /// </summary>
    public const string CartKindProperty = AgentCheckout.KindProperty;

    /// <summary>
    /// Maps the catalog and cart endpoints.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    public static IEndpointRouteBuilder MapUcpCatalog(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(UcpConstants.RoutePrefix)
            .AllowAnonymous()
            .DisableAntiforgery()
            .AddEndpointFilter<UcpRequestFilter>();

        group.MapPost("catalog/search", SearchAsync).WithName("UcpCatalogSearch");
        group.MapPost("catalog/lookup", LookupAsync).WithName("UcpCatalogLookup");
        group.MapPost("catalog/product", ProductAsync).WithName("UcpCatalogProduct");
        group.MapPost("carts", CreateCartAsync).WithIdempotency(UcpEndpoints.Idempotency).WithName("UcpCreateCart");
        group.MapGet("carts/{id}", GetCartAsync).WithName("UcpGetCart");
        group.MapPut("carts/{id}", UpdateCartAsync).WithIdempotency(UcpEndpoints.Idempotency).WithName("UcpUpdateCart");
        group.MapPost("carts/{id}/cancel", CancelCartAsync).WithIdempotency(UcpEndpoints.Idempotency).WithName("UcpCancelCart");

        return routes;
    }

    private static async Task<IResult> SearchAsync(HttpContext httpContext, UcpCatalog catalog)
    {
        var request = AgentRequest.Get(httpContext);
        var body = request.Json as JsonObject ?? [];
        var limit = AgentJson.Int(body["pagination"], "limit") ?? 20;
        var skip = int.TryParse(AgentJson.String(body["pagination"], "cursor"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset) ? offset : 0;
        var (products, total) = await catalog.SearchAsync(AgentJson.String(body, "query"), skip, limit, AgentJson.String(body["context"], "currency"), httpContext.RequestAborted);
        var json = new JsonObject
        {
            ["ucp"] = Ucp(request, UcpConstants.Capabilities.CatalogSearch),
            ["products"] = products,
        };

        var pagination = new JsonObject { ["total_count"] = total };

        if (skip + products.Count < total)
        {
            pagination["next_cursor"] = (skip + Math.Clamp(limit, 1, 50)).ToString(CultureInfo.InvariantCulture);
        }

        json["pagination"] = pagination;

        return UcpResults.Json(json);
    }

    private static async Task<IResult> LookupAsync(HttpContext httpContext, UcpCatalog catalog, IAgentItemResolver items)
    {
        var request = AgentRequest.Get(httpContext);
        var ids = AgentJson.Strings(request.Json, "ids") ?? [];
        var contentItemIds = new List<string>();

        foreach (var id in ids.Take(50))
        {
            if (await items.ResolveAsync(id, httpContext.RequestAborted) is { } item)
            {
                contentItemIds.Add(item.ContentItemId);
            }
            else
            {
                contentItemIds.Add(id);
            }
        }

        return UcpResults.Json(new JsonObject
        {
            ["ucp"] = Ucp(request, UcpConstants.Capabilities.CatalogLookup),
            ["products"] = await catalog.WriteAsync(contentItemIds, AgentJson.String(request.Json?["context"], "currency"), httpContext.RequestAborted),
        });
    }

    private static async Task<IResult> ProductAsync(HttpContext httpContext, UcpCatalog catalog, IAgentItemResolver items)
    {
        var request = AgentRequest.Get(httpContext);
        var id = AgentJson.String(request.Json, "id");

        if (string.IsNullOrWhiteSpace(id))
        {
            return UcpResults.Error(StatusCodes.Status400BadRequest, "invalid_request", "Give the product's id.");
        }

        var contentItemId = (await items.ResolveAsync(id, httpContext.RequestAborted))?.ContentItemId ?? id;
        var products = await catalog.WriteAsync([contentItemId], AgentJson.String(request.Json?["context"], "currency"), httpContext.RequestAborted);

        if (products.Count == 0)
        {
            return UcpResults.Error(StatusCodes.Status404NotFound, "not_found", "There is no such product.");
        }

        return UcpResults.Json(new JsonObject
        {
            ["ucp"] = Ucp(request, UcpConstants.Capabilities.CatalogLookup),
            ["product"] = products[0].DeepClone(),
        });
    }

    private static async Task<IResult> CreateCartAsync(HttpContext httpContext, IAgentCheckoutService checkouts, IAgentCheckoutStore store, ISiteService siteService, IClock clock)
    {
        var request = AgentRequest.Get(httpContext);

        if (request.Json is not JsonObject body)
        {
            return UcpResults.Error(StatusCodes.Status400BadRequest, "invalid_request", "Send the cart as a JSON object.");
        }

        // Carts count against the platform's cap on open checkouts, so an agent can't keep making them without end.
        var settings = await siteService.GetSettingsAsync<AgenticCommerceSettings>();

        if (await store.CountOpenAsync(request.Platform.ItemId, httpContext.RequestAborted) >= Math.Max(1, settings.MaxOpenCheckoutsPerPlatform))
        {
            return UcpResults.Error(StatusCodes.Status429TooManyRequests, "rate_limited", "Too many carts and checkouts are open. Complete or cancel some first.");
        }

        var now = clock.UtcNow;
        var cart = new AgentCheckout
        {
            ItemId = IdGenerator.GenerateId(),
            PlatformId = request.Platform.ItemId,
            Protocol = AgenticCommerceConstants.Protocols.Ucp,
            ProtocolVersion = request.Version,
            UserId = request.Context.UserId,
            Lines = UcpCheckoutMapper.ReadInput(body).Lines.ToList(),
            CreatedUtc = now,
            ModifiedUtc = now,
            ExpiresUtc = now.AddDays(1),
        };

        cart.SetProperty(CartKindProperty, AgentCheckout.CartKind);
        await store.CreateAsync(cart, httpContext.RequestAborted);

        return UcpResults.Json(await WriteCartAsync(request, cart, checkouts, httpContext.RequestAborted), StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetCartAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, IAgentCheckoutStore store)
    {
        var request = AgentRequest.Get(httpContext);

        if (await FindCartAsync(store, request, id, httpContext.RequestAborted) is not { } cart)
        {
            return UcpResults.Error(StatusCodes.Status404NotFound, "not_found", "There is no such cart.");
        }

        return UcpResults.Json(await WriteCartAsync(request, cart, checkouts, httpContext.RequestAborted));
    }

    private static async Task<IResult> UpdateCartAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, IAgentCheckoutStore store, IClock clock)
    {
        var request = AgentRequest.Get(httpContext);

        if (request.Json is not JsonObject body || await FindCartAsync(store, request, id, httpContext.RequestAborted) is not { } cart)
        {
            return UcpResults.Error(StatusCodes.Status404NotFound, "not_found", "There is no such cart.");
        }

        if (cart.IsTerminal)
        {
            return UcpResults.Json(UcpCheckoutMapper.WriteError(request, "The cart was canceled.", AgentMessageCodes.InvalidState));
        }

        cart.Lines = UcpCheckoutMapper.ReadInput(body).Lines.ToList();
        cart.ModifiedUtc = clock.UtcNow;
        await store.UpdateAsync(cart, httpContext.RequestAborted);

        return UcpResults.Json(await WriteCartAsync(request, cart, checkouts, httpContext.RequestAborted));
    }

    private static async Task<IResult> CancelCartAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, IAgentCheckoutStore store, IClock clock)
    {
        var request = AgentRequest.Get(httpContext);

        if (await FindCartAsync(store, request, id, httpContext.RequestAborted) is not { } cart)
        {
            return UcpResults.Error(StatusCodes.Status404NotFound, "not_found", "There is no such cart.");
        }

        cart.Status = AgentCheckoutStatus.Canceled;
        cart.ModifiedUtc = clock.UtcNow;
        await store.UpdateAsync(cart, httpContext.RequestAborted);

        return UcpResults.Json(await WriteCartAsync(request, cart, checkouts, httpContext.RequestAborted));
    }

    /// <summary>
    /// Returns a platform's cart, or <see langword="null"/>.
    /// </summary>
    /// <param name="store">The agent checkout store.</param>
    /// <param name="request">The request.</param>
    /// <param name="id">The cart's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<AgentCheckout> FindCartAsync(IAgentCheckoutStore store, AgentRequest request, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var cart = await store.FindByIdAsync(id, cancellationToken);

        return cart is not null &&
            cart.GetProperty(CartKindProperty) == AgentCheckout.CartKind &&
            string.Equals(cart.PlatformId, request.Platform.ItemId, StringComparison.Ordinal)
            ? cart
            : null;
    }

    private static async Task<JsonObject> WriteCartAsync(AgentRequest request, AgentCheckout cart, IAgentCheckoutService checkouts, CancellationToken cancellationToken)
    {
        var view = await checkouts.PreviewAsync(request.Context, cart.Lines, cancellationToken);
        var lines = new JsonArray();

        foreach (var line in view.Lines)
        {
            lines.Add(new JsonObject
            {
                ["id"] = line.Id,
                ["item"] = new JsonObject
                {
                    ["id"] = line.ItemId,
                    ["title"] = line.Title,
                    ["price"] = AgentJson.ToMinor(line.UnitPrice, view.Currency),
                },
                ["quantity"] = line.Quantity,
                ["totals"] = new JsonArray(new JsonObject { ["type"] = "total", ["amount"] = AgentJson.ToMinor(line.Total, view.Currency) }),
            });
        }

        var json = new JsonObject
        {
            ["ucp"] = Ucp(request, UcpConstants.Capabilities.Cart),
            ["id"] = cart.ItemId,
            ["currency"] = view.Currency?.ToUpperInvariant(),
            ["line_items"] = lines,
            ["totals"] = UcpCheckoutMapper.WriteTotals(view.Totals, view.Currency),
            ["expires_at"] = AgentJson.ToRfc3339(cart.ExpiresUtc),
        };

        if (cart.Status == AgentCheckoutStatus.Canceled)
        {
            json["status"] = "canceled";
        }

        if (view.Messages.Count > 0)
        {
            json["messages"] = new JsonArray([.. view.Messages.Select(message => (JsonNode)UcpCheckoutMapper.WriteMessage(message))]);
        }

        return json;
    }

    private static JsonObject Ucp(AgentRequest request, string capability)
        => new()
        {
            ["version"] = request.Version,
            ["capabilities"] = new JsonObject
            {
                [capability] = new JsonArray(new JsonObject { ["version"] = request.Version }),
            },
        };
}
