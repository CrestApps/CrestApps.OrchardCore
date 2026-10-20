using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Api;

/// <summary>
/// The store's coupons and automatic discounts over the API, for staff with Manage coupons, held to the same rules as
/// the coupon admin.
/// </summary>
internal static class CouponAdminApiEndpoints
{
    public const string Tag = "Coupons";

    public static IEndpointRouteBuilder MapCouponAdminApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("coupons", ListAsync)
            .WithName("ListCoupons")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the store's coupons and automatic discounts, searched by code or description.");

        group.MapGet("coupons/{couponId}", GetAsync)
            .WithName("GetCoupon")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets a coupon.");

        group.MapPost("coupons", CreateAsync)
            .WithIdempotency()
            .WithName("CreateCoupon")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Creates a coupon, or an automatic discount.");

        group.MapPut("coupons/{couponId}", UpdateAsync)
            .WithName("UpdateCoupon")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Changes a coupon. What's not sent is cleared, as when the coupon's form is saved.");

        group.MapDelete("coupons/{couponId}", DeleteAsync)
            .WithName("DeleteCoupon")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Deletes a coupon. Orders that used it keep their discount.");

        return routes;
    }

    private static async Task<Results<Ok<ApiPage<ApiCoupon>>, ProblemHttpResult>> ListAsync(
        string q,
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICouponStore coupons,
        IClock clock)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CheckoutPermissions.ManageCoupons))
        {
            return ApiProblems.Forbidden();
        }

        // A site has tens of coupons, not thousands, so they're searched and paged in memory, as the admin does.
        var search = q?.Trim();
        var matching = (await coupons.GetAllAsync())
            .Where(coupon => string.IsNullOrEmpty(search) ||
                (coupon.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (coupon.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(coupon => coupon.CreatedUtc)
            .ToArray();
        var pageSkip = ApiPage<ApiCoupon>.ClampSkip(skip);
        var pageTake = ApiPage<ApiCoupon>.ClampTake(take);
        var now = clock.UtcNow;

        return TypedResults.Ok(new ApiPage<ApiCoupon>(matching.Skip(pageSkip).Take(pageTake).Select(coupon => ToApi(coupon, now)).ToList(), matching.Length, pageSkip, pageTake));
    }

    private static async Task<Results<Ok<ApiCoupon>, ProblemHttpResult>> GetAsync(
        string couponId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICouponStore coupons,
        IClock clock)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CheckoutPermissions.ManageCoupons))
        {
            return ApiProblems.Forbidden();
        }

        return await coupons.FindByIdAsync(couponId) is { } coupon
            ? TypedResults.Ok(ToApi(coupon, clock.UtcNow))
            : ApiProblems.NotFound();
    }

    private static async Task<Results<Created<ApiCoupon>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        ApiCouponRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICouponStore coupons,
        CouponEditor editor,
        IClock clock)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CheckoutPermissions.ManageCoupons))
        {
            return ApiProblems.Forbidden();
        }

        var model = ToModel(request);
        var modelState = new ModelStateDictionary();

        await editor.ValidateAsync(model, existingItemId: null, modelState);

        if (!modelState.IsValid)
        {
            return ApiProblems.Validation(modelState);
        }

        var now = clock.UtcNow;
        var coupon = new Coupon { ItemId = IdGenerator.GenerateId(), CreatedUtc = now, UpdatedUtc = now };

        CouponEditor.Apply(model, coupon);
        ApplyLimits(request, coupon);

        await coupons.CreateAsync(coupon);

        return TypedResults.Created($"{httpContext.Request.PathBase}/{CommerceApi.RoutePrefix}/coupons/{coupon.ItemId}", ToApi(coupon, now));
    }

    private static async Task<Results<Ok<ApiCoupon>, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        string couponId,
        ApiCouponRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICouponStore coupons,
        CouponEditor editor,
        IClock clock)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CheckoutPermissions.ManageCoupons))
        {
            return ApiProblems.Forbidden();
        }

        if (await coupons.FindByIdAsync(couponId) is not { } coupon)
        {
            return ApiProblems.NotFound();
        }

        var model = ToModel(request);
        var modelState = new ModelStateDictionary();

        await editor.ValidateAsync(model, coupon.ItemId, modelState);

        if (!modelState.IsValid)
        {
            return ApiProblems.Validation(modelState);
        }

        CouponEditor.Apply(model, coupon);
        ApplyLimits(request, coupon);
        coupon.UpdatedUtc = clock.UtcNow;

        await coupons.UpdateAsync(coupon);

        return TypedResults.Ok(ToApi(coupon, clock.UtcNow));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        string couponId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICouponStore coupons)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CheckoutPermissions.ManageCoupons))
        {
            return ApiProblems.Forbidden();
        }

        if (await coupons.FindByIdAsync(couponId) is not { } coupon)
        {
            return ApiProblems.NotFound();
        }

        await coupons.DeleteAsync(coupon);
        await coupons.DeleteGeneratedCodesAsync(coupon.ItemId, httpContext.RequestAborted);

        return TypedResults.NoContent();
    }

    private static CouponEditViewModel ToModel(ApiCouponRequest request)
        => new()
        {
            Code = request?.Code,
            Method = string.Equals(request?.Method, "automatic", StringComparison.OrdinalIgnoreCase) ? CouponMethod.Automatic : CouponMethod.Code,
            OnlyGeneratedCodes = request?.OnlyGeneratedCodes == true,
            Description = request?.Description,
            Kind = ParseKind(request?.Kind),
            Percentage = request?.Percentage ?? 0m,
            Amount = request?.Amount ?? 0m,
            Currency = request?.Currency,
            Target = string.Equals(request?.Target, "firstCycle", StringComparison.OrdinalIgnoreCase) ? DiscountTarget.FirstCycle : DiscountTarget.OneTime,
            IsEnabled = request?.IsEnabled != false,
            StartsUtc = request?.StartsUtc,
            EndsUtc = request?.EndsUtc,
            MaxRedemptions = request?.MaxRedemptions,
            MaxRedemptionsPerCustomer = request?.MaxRedemptionsPerCustomer,
            MinimumAmount = request?.MinimumAmount,
            MaximumAmount = request?.MaximumAmount,
            MinimumQuantity = request?.MinimumQuantity,
            BuyQuantity = request?.BuyQuantity,
            GetQuantity = request?.GetQuantity,
            MaxApplicationsPerOrder = request?.MaxApplicationsPerOrder,
            CombinesWithProductDiscounts = request?.CombinesWithProductDiscounts == true,
            CombinesWithOrderDiscounts = request?.CombinesWithOrderDiscounts == true,
            CombinesWithShippingDiscounts = request?.CombinesWithShippingDiscounts == true,
            CustomerId = request?.CustomerId,
            CustomerEmail = request?.CustomerEmail,
            Sources = request?.Sources is { Count: > 0 } sources ? string.Join(',', sources) : null,
            ReferenceTypes = request?.ReferenceTypes is { Count: > 0 } types ? string.Join(',', types) : null,
        };

    // What the coupon admin takes from the product pickers other modules add to its form.
    private static void ApplyLimits(ApiCouponRequest request, Coupon coupon)
    {
        coupon.ProductIds = Clean(request?.ProductIds);
        coupon.CategoryIds = Clean(request?.CategoryIds);

        if (coupon.Kind == CouponKind.BuyXGetY)
        {
            coupon.BuyProductIds = Clean(request?.BuyProductIds);
            coupon.BuyCategoryIds = Clean(request?.BuyCategoryIds);
        }
    }

    private static List<string> Clean(IEnumerable<string> ids)
        => ids?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList() ?? [];

    private static CouponKind ParseKind(string kind)
        => kind?.ToLowerInvariant() switch
        {
            "fixedamount" or "fixed-amount" => CouponKind.FixedAmount,
            "freeshipping" or "free-shipping" => CouponKind.FreeShipping,
            "buyxgety" or "buy-x-get-y" => CouponKind.BuyXGetY,
            _ => CouponKind.Percentage,
        };

    private static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    internal static ApiCoupon ToApi(Coupon coupon, DateTime now)
        => new(
            coupon.ItemId,
            coupon.Code,
            coupon.IsAutomatic ? "automatic" : "code",
            coupon.Description,
            ToCamel(coupon.Kind.ToString()),
            coupon.Kind is CouponKind.Percentage or CouponKind.BuyXGetY ? coupon.Percentage : null,
            coupon.Kind == CouponKind.FixedAmount ? ApiMoney.From(coupon.Amount, coupon.Currency) : null,
            ToCamel(coupon.Target.ToString()),
            coupon.IsEnabled,
            coupon.IsEnabled && coupon.IsRedeemable(now),
            coupon.StartsUtc,
            coupon.EndsUtc,
            coupon.MaxRedemptions,
            coupon.MaxRedemptionsPerCustomer,
            coupon.RedemptionCount,
            coupon.MinimumAmount,
            coupon.MaximumAmount,
            coupon.MinimumQuantity,
            coupon.Kind == CouponKind.BuyXGetY ? coupon.BuyQuantity : null,
            coupon.Kind == CouponKind.BuyXGetY ? coupon.GetQuantity : null,
            coupon.MaxApplicationsPerOrder,
            coupon.CombinesWithProductDiscounts,
            coupon.CombinesWithOrderDiscounts,
            coupon.CombinesWithShippingDiscounts,
            coupon.OnlyGeneratedCodes,
            [.. coupon.ProductIds ?? []],
            [.. coupon.CategoryIds ?? []],
            [.. coupon.BuyProductIds ?? []],
            [.. coupon.BuyCategoryIds ?? []],
            coupon.CustomerId,
            coupon.CustomerEmail,
            coupon.CreatedUtc);
}

/// <summary>
/// A coupon, or an automatic discount.
/// </summary>
/// <param name="Method"><c>code</c>, typed by the buyer; or <c>automatic</c>, taken off whenever it applies.</param>
/// <param name="Kind"><c>percentage</c>, <c>fixedAmount</c>, <c>freeShipping</c> or <c>buyXGetY</c>.</param>
/// <param name="Target">What it comes off: <c>oneTime</c> (goods paid now) or <c>firstCycle</c> (a subscription's first charge).</param>
/// <param name="IsRedeemable">Whether it can be used now: enabled, within its dates, and not used up.</param>
public sealed record ApiCoupon(
    string Id,
    string Code,
    string Method,
    string Description,
    string Kind,
    decimal? Percentage,
    ApiMoney Amount,
    string Target,
    bool IsEnabled,
    bool IsRedeemable,
    DateTime? StartsUtc,
    DateTime? EndsUtc,
    int? MaxRedemptions,
    int? MaxRedemptionsPerCustomer,
    int RedemptionCount,
    decimal? MinimumAmount,
    decimal? MaximumAmount,
    int? MinimumQuantity,
    int? BuyQuantity,
    int? GetQuantity,
    int? MaxApplicationsPerOrder,
    bool CombinesWithProductDiscounts,
    bool CombinesWithOrderDiscounts,
    bool CombinesWithShippingDiscounts,
    bool OnlyGeneratedCodes,
    IReadOnlyList<string> ProductIds,
    IReadOnlyList<string> CategoryIds,
    IReadOnlyList<string> BuyProductIds,
    IReadOnlyList<string> BuyCategoryIds,
    string CustomerId,
    string CustomerEmail,
    DateTime CreatedUtc);

/// <summary>
/// A coupon to create or save.
/// </summary>
public sealed class ApiCouponRequest
{
    /// <summary>
    /// The code buyers type. An automatic discount left without one is given one.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// <c>code</c> (the default) or <c>automatic</c>.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Whether only the coupon's generated codes work, not the code itself.
    /// </summary>
    public bool? OnlyGeneratedCodes { get; set; }

    /// <summary>
    /// What the buyer sees; required for an automatic discount.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// <c>percentage</c> (the default), <c>fixedAmount</c>, <c>freeShipping</c> or <c>buyXGetY</c>.
    /// </summary>
    public string Kind { get; set; }

    public decimal? Percentage { get; set; }

    public decimal? Amount { get; set; }

    /// <summary>
    /// The currency of a fixed amount.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// <c>oneTime</c> (the default) or <c>firstCycle</c>.
    /// </summary>
    public string Target { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? StartsUtc { get; set; }

    public DateTime? EndsUtc { get; set; }

    public int? MaxRedemptions { get; set; }

    public int? MaxRedemptionsPerCustomer { get; set; }

    public decimal? MinimumAmount { get; set; }

    public decimal? MaximumAmount { get; set; }

    public int? MinimumQuantity { get; set; }

    public int? BuyQuantity { get; set; }

    public int? GetQuantity { get; set; }

    public int? MaxApplicationsPerOrder { get; set; }

    public bool? CombinesWithProductDiscounts { get; set; }

    public bool? CombinesWithOrderDiscounts { get; set; }

    public bool? CombinesWithShippingDiscounts { get; set; }

    /// <summary>
    /// The products it's limited to.
    /// </summary>
    public List<string> ProductIds { get; set; }

    /// <summary>
    /// The categories it's limited to.
    /// </summary>
    public List<string> CategoryIds { get; set; }

    /// <summary>
    /// For buy X get Y, the products that count toward what's bought.
    /// </summary>
    public List<string> BuyProductIds { get; set; }

    /// <summary>
    /// For buy X get Y, the categories that count toward what's bought.
    /// </summary>
    public List<string> BuyCategoryIds { get; set; }

    /// <summary>
    /// The one customer who may use it.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// The one email that may use it.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// The transaction sources it works for, such as a sales channel.
    /// </summary>
    public List<string> Sources { get; set; }

    /// <summary>
    /// The kinds of checkout it works for, such as Order.
    /// </summary>
    public List<string> ReferenceTypes { get; set; }
}
