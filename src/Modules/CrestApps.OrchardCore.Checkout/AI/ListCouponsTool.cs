using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.AI;

/// <summary>
/// Lists coupons and automatic discounts as Commerce › Coupons does: searched by code or description, filtered by active,
/// not available or disabled, newest first, and with the list's warning about enabled coupons that have neither a use
/// limit nor an end date. Generated codes are not listed.
/// </summary>
internal sealed class ListCouponsTool : CouponAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listCoupons";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text found in the coupon's code or description."
        },
        "status": {
          "type": "string",
          "enum": ["all", "active", "notAvailable", "disabled"],
          "description": "all (the default); active: enabled and usable now; notAvailable: enabled but not usable now (not started, ended or used up); disabled."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Coupons per page: 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists coupons and automatic discounts with code, description, what they take off, dates, use limit and uses so far, and whether each can be used now. Filters by text and by active, not available or disabled, with paging; it also flags enabled coupons that have neither a use limit nor an end date.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var status = ReadString(arguments, "status") ?? "all";

        if (status is not ("all" or "active" or "notAvailable" or "disabled"))
        {
            return "Nothing was listed: status must be all, active, notAvailable or disabled.";
        }

        // A site has tens of coupons, not thousands, so they are filtered and paged in memory, as the coupon list does.
        var now = services.GetRequiredService<IClock>().UtcNow;
        var all = (await services.GetRequiredService<ICouponStore>().GetAllAsync(cancellationToken)).ToArray();
        IEnumerable<Coupon> coupons = all;

        if (ReadString(arguments, "search") is { } search)
        {
            coupons = coupons.Where(coupon =>
                (coupon.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (coupon.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        coupons = status switch
        {
            "active" => coupons.Where(coupon => coupon.IsEnabled && coupon.IsRedeemable(now)),
            "notAvailable" => coupons.Where(coupon => coupon.IsEnabled && !coupon.IsRedeemable(now)),
            "disabled" => coupons.Where(coupon => !coupon.IsEnabled),
            _ => coupons,
        };

        var matching = coupons.OrderByDescending(coupon => coupon.CreatedUtc).ToArray();
        var (page, pageSize) = ReadPaging(arguments);
        var items = new List<object>();

        foreach (var coupon in matching.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await SummarizeAsync(services, coupon, now));
        }

        var unbounded = all.Count(IsUnbounded);

        return Json(new
        {
            items,
            total = matching.Length,
            page,
            pageSize,
            truncated = matching.Length > page * pageSize,
            unboundedCoupons = unbounded,
            warning = unbounded > 0
                ? $"{unbounded} enabled coupon(s) have neither a use limit nor an end date, so a code that leaks keeps discounting. They are marked unbounded."
                : null,
        });
    }
}
