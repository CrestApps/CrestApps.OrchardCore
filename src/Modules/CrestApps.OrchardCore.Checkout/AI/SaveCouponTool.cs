using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.AI;

/// <summary>
/// Creates or changes a coupon or automatic discount with the coupon editor's rules, as the Coupon API does
/// (<see cref="CouponEditor.ValidateAsync"/> and <see cref="CouponEditor.Apply"/>, with the product and category limits
/// set as the API sets them). A change starts from the stored coupon, as its editor opens it, so only the fields given
/// change; dates are taken in the site's time zone, as the editor takes them. It never changes how often a coupon was
/// used. It always asks for approval: a coupon is a discount anyone with the code can take, and approval is fixed when
/// the tool is registered, so it cannot ask only for coupons left without a use limit or an end date.
/// </summary>
internal sealed class SaveCouponTool : CouponAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveCoupon";

    private static readonly string[] _dateFormats = ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "couponId": {
          "type": "string",
          "description": "The coupon to change, from listCoupons. Leave it out to create one."
        },
        "code": {
          "type": "string",
          "description": "The code buyers type, unique among coupons. Required for a new coupon; an automatic discount left without one is given one."
        },
        "method": {
          "type": "string",
          "enum": ["code", "automatic"],
          "description": "code: the buyer types it (the default); automatic: taken off by itself whenever it applies."
        },
        "onlyGeneratedCodes": {
          "type": "boolean",
          "description": "Whether only the codes generated for the coupon work, not its own code."
        },
        "description": {
          "type": "string",
          "description": "What the buyer sees; required for an automatic discount."
        },
        "kind": {
          "type": "string",
          "enum": ["percentage", "fixedAmount", "freeShipping", "buyXGetY"],
          "description": "How the discount is worked out. A new coupon is a percentage when left out."
        },
        "percentage": {
          "type": "number",
          "description": "The percentage off, 1 to 100, for percentage and buy X get Y."
        },
        "amount": {
          "type": "number",
          "description": "The amount off, more than zero, for a fixed amount."
        },
        "currency": {
          "type": "string",
          "description": "The currency of a fixed amount, such as USD; required with it."
        },
        "target": {
          "type": "string",
          "enum": ["oneTime", "firstCycle"],
          "description": "What it comes off: oneTime (goods paid now, the default) or firstCycle (a subscription's first charge)."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the coupon can be used. A new coupon is enabled when left out."
        },
        "starts": {
          "type": "string",
          "description": "When it can first be used, in the site's time zone, as yyyy-MM-ddTHH:mm (a date alone means 00:00), or 'none' to clear."
        },
        "ends": {
          "type": "string",
          "description": "When it stops, in the site's time zone, as yyyy-MM-ddTHH:mm (a date alone means 00:00 that day, so give 23:59 to include the day), or 'none' to clear."
        },
        "maxUses": {
          "type": "integer",
          "description": "How many times it can be used in all; 0 removes the limit."
        },
        "maxUsesPerCustomer": {
          "type": "integer",
          "description": "How many times one customer can use it; 0 removes the limit."
        },
        "minimumAmount": {
          "type": "number",
          "description": "The least the order must come to; 0 removes it."
        },
        "maximumAmount": {
          "type": "number",
          "description": "The most the order may come to; 0 removes it."
        },
        "minimumQuantity": {
          "type": "integer",
          "description": "The fewest items the order must have; 0 removes it."
        },
        "buyQuantity": {
          "type": "integer",
          "description": "For buy X get Y: how many items the customer buys, 1 or more."
        },
        "getQuantity": {
          "type": "integer",
          "description": "For buy X get Y: how many items the customer gets at the percentage off, 1 or more."
        },
        "maxApplicationsPerOrder": {
          "type": "integer",
          "description": "For buy X get Y: how often it applies in one order; 0 for no limit."
        },
        "combinesWithProductDiscounts": {
          "type": "boolean",
          "description": "Whether it can be used with product discounts."
        },
        "combinesWithOrderDiscounts": {
          "type": "boolean",
          "description": "Whether it can be used with other order discounts."
        },
        "combinesWithShippingDiscounts": {
          "type": "boolean",
          "description": "Whether it can be used with shipping discounts."
        },
        "productIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The product content item ids it is limited to; an empty list removes the limit."
        },
        "categoryIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The category ids it is limited to; an empty list removes the limit."
        },
        "buyProductIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "For buy X get Y: the products that count toward what is bought."
        },
        "buyCategoryIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "For buy X get Y: the categories that count toward what is bought."
        },
        "customerId": {
          "type": "string",
          "description": "The one customer who may use it, or 'none' to clear."
        },
        "customerEmail": {
          "type": "string",
          "description": "The one email that may use it, or 'none' to clear."
        },
        "sources": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The transaction sources (sales channels) it works for; an empty list means all."
        },
        "referenceTypes": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The kinds of checkout it works for, such as Order; an empty list means all."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or changes a coupon or automatic discount: code, percentage or fixed amount, what it applies to, dates in the site's time zone, use limits, spend and quantity conditions, buy X get Y, product, category, customer and channel limits, and what it combines with. It is checked with the same rules as the coupon editor and never changes how often a coupon was used.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        Coupon coupon = null;

        if (ReadString(arguments, "couponId") is { } couponId)
        {
            (coupon, var findError) = await FindCouponAsync(services, couponId, code: null, cancellationToken);

            if (coupon is null)
            {
                return findError;
            }
        }

        // A change starts from the stored coupon, as its editor opens it; a new coupon from the editor's blank form.
        var model = coupon is null ? new CouponEditViewModel { IsEnabled = true } : CouponEditor.ToModel(coupon);

        if (await ApplyArgumentsAsync(arguments, model, services) is { } argumentError)
        {
            return argumentError;
        }

        var modelState = new ModelStateDictionary();

        await services.GetRequiredService<CouponEditor>().ValidateAsync(model, coupon?.ItemId, modelState);

        if (!modelState.IsValid)
        {
            var problems = modelState
                .Where(entry => entry.Value.Errors.Count > 0)
                .SelectMany(entry => entry.Value.Errors.Select(problem => $"{ArgumentName(entry.Key)}: {problem.ErrorMessage}"));

            return $"Nothing was saved: {string.Join(" ", problems)}";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var store = services.GetRequiredService<ICouponStore>();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var isNew = coupon is null;

        coupon ??= new Coupon { ItemId = IdGenerator.GenerateId(), CreatedUtc = now };

        CouponEditor.Apply(model, coupon);
        ApplyLimits(arguments, coupon);
        coupon.UpdatedUtc = now;

        if (isNew)
        {
            await store.CreateAsync(coupon, cancellationToken);
        }
        else
        {
            await store.UpdateAsync(coupon, cancellationToken);
        }

        if (await CommitAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        return Json(new
        {
            coupon = await DescribeAsync(services, coupon, now),
            message = isNew ? $"Coupon {coupon.Code} was created." : $"Coupon {coupon.Code} was updated.",
            warning = IsUnbounded(coupon)
                ? "This coupon is enabled with neither a use limit nor an end date, so a code that leaks keeps discounting. Consider setting maxUses or ends."
                : null,
            adminPage = GetAdminPath(services, "CouponsEdit", new { itemId = coupon.ItemId }),
        });
    }

    // Overlays the arguments given on the form's values. Kinds, methods and targets the editor offers are mapped the way
    // the Coupon API maps them.
    private static async Task<string> ApplyArgumentsAsync(AIFunctionArguments arguments, CouponEditViewModel model, IServiceProvider services)
    {
        if (ReadString(arguments, "code") is { } code)
        {
            model.Code = code;
        }

        if (ReadString(arguments, "method") is { } method)
        {
            if (method.ToLowerInvariant() is not ("code" or "automatic"))
            {
                return "Nothing was saved: method must be code or automatic.";
            }

            model.Method = string.Equals(method, "automatic", StringComparison.OrdinalIgnoreCase) ? CouponMethod.Automatic : CouponMethod.Code;
        }

        if (ReadBool(arguments, "onlyGeneratedCodes") is { } onlyGenerated)
        {
            model.OnlyGeneratedCodes = onlyGenerated;
        }

        if (ReadString(arguments, "description") is { } description)
        {
            model.Description = description;
        }

        if (ReadString(arguments, "kind") is { } kind)
        {
            CouponKind? parsed = kind.ToLowerInvariant() switch
            {
                "percentage" => CouponKind.Percentage,
                "fixedamount" => CouponKind.FixedAmount,
                "freeshipping" => CouponKind.FreeShipping,
                "buyxgety" => CouponKind.BuyXGetY,
                _ => null,
            };

            if (parsed is null)
            {
                return "Nothing was saved: kind must be percentage, fixedAmount, freeShipping or buyXGetY.";
            }

            model.Kind = parsed.Value;
        }

        if (ReadString(arguments, "target") is { } target)
        {
            if (target.ToLowerInvariant() is not ("onetime" or "firstcycle"))
            {
                return "Nothing was saved: target must be oneTime or firstCycle.";
            }

            model.Target = string.Equals(target, "firstCycle", StringComparison.OrdinalIgnoreCase) ? DiscountTarget.FirstCycle : DiscountTarget.OneTime;
        }

        if (ReadBool(arguments, "enabled") is { } enabled)
        {
            model.IsEnabled = enabled;
        }

        if (!TryReadDecimal(arguments, "percentage", out var percentage) ||
            !TryReadDecimal(arguments, "amount", out var amount) ||
            !TryReadDecimal(arguments, "minimumAmount", out var minimumAmount) ||
            !TryReadDecimal(arguments, "maximumAmount", out var maximumAmount))
        {
            return "Nothing was saved: percentage, amount, minimumAmount and maximumAmount must be numbers.";
        }

        if (!TryReadInt(arguments, "maxUses", out var maxUses) ||
            !TryReadInt(arguments, "maxUsesPerCustomer", out var maxUsesPerCustomer) ||
            !TryReadInt(arguments, "minimumQuantity", out var minimumQuantity) ||
            !TryReadInt(arguments, "buyQuantity", out var buyQuantity) ||
            !TryReadInt(arguments, "getQuantity", out var getQuantity) ||
            !TryReadInt(arguments, "maxApplicationsPerOrder", out var maxApplications))
        {
            return "Nothing was saved: maxUses, maxUsesPerCustomer, minimumQuantity, buyQuantity, getQuantity and maxApplicationsPerOrder must be whole numbers.";
        }

        if (percentage is { } percent)
        {
            model.Percentage = percent;
        }

        if (amount is { } fixedAmount)
        {
            model.Amount = fixedAmount;
        }

        if (ReadString(arguments, "currency") is { } currency)
        {
            model.Currency = currency;
        }

        // Zero clears an optional limit, as leaving the editor's field blank does.
        model.MaxRedemptions = Limit(maxUses, model.MaxRedemptions);
        model.MaxRedemptionsPerCustomer = Limit(maxUsesPerCustomer, model.MaxRedemptionsPerCustomer);
        model.MinimumQuantity = Limit(minimumQuantity, model.MinimumQuantity);
        model.MaxApplicationsPerOrder = Limit(maxApplications, model.MaxApplicationsPerOrder);
        model.MinimumAmount = minimumAmount is { } minimum ? (minimum == 0m ? (decimal?)null : minimum) : model.MinimumAmount;
        model.MaximumAmount = maximumAmount is { } maximum ? (maximum == 0m ? (decimal?)null : maximum) : model.MaximumAmount;
        model.BuyQuantity = buyQuantity ?? model.BuyQuantity;
        model.GetQuantity = getQuantity ?? model.GetQuantity;

        if (ReadBool(arguments, "combinesWithProductDiscounts") is { } withProducts)
        {
            model.CombinesWithProductDiscounts = withProducts;
        }

        if (ReadBool(arguments, "combinesWithOrderDiscounts") is { } withOrders)
        {
            model.CombinesWithOrderDiscounts = withOrders;
        }

        if (ReadBool(arguments, "combinesWithShippingDiscounts") is { } withShipping)
        {
            model.CombinesWithShippingDiscounts = withShipping;
        }

        if (ReadString(arguments, "customerId") is { } customerId)
        {
            model.CustomerId = IsNone(customerId) ? null : customerId;
        }

        if (ReadString(arguments, "customerEmail") is { } customerEmail)
        {
            model.CustomerEmail = IsNone(customerEmail) ? null : customerEmail;
        }

        if (ReadStrings(arguments, "sources") is { } sources)
        {
            model.Sources = string.Join(',', sources);
        }

        if (ReadStrings(arguments, "referenceTypes") is { } referenceTypes)
        {
            model.ReferenceTypes = string.Join(',', referenceTypes);
        }

        // The editor takes the dates in the site's time zone and keeps them in UTC.
        var localClock = services.GetRequiredService<ILocalClock>();
        var (startsGiven, startsUtc, startsError) = await ReadLocalDateAsync(arguments, "starts", localClock);
        var (endsGiven, endsUtc, endsError) = await ReadLocalDateAsync(arguments, "ends", localClock);

        if ((startsError ?? endsError) is { } dateError)
        {
            return dateError;
        }

        if (startsGiven)
        {
            model.StartsUtc = startsUtc;
        }

        if (endsGiven)
        {
            model.EndsUtc = endsUtc;
        }

        return null;
    }

    // A date and time in the site's time zone, converted to UTC as the editor converts it; 'none' clears it.
    private static async Task<(bool Given, DateTime? Utc, string Error)> ReadLocalDateAsync(AIFunctionArguments arguments, string name, ILocalClock localClock)
    {
        if (ReadString(arguments, name) is not { } text)
        {
            return (false, null, null);
        }

        if (IsNone(text))
        {
            return (true, null, null);
        }

        if (!DateTime.TryParseExact(text, _dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return (false, null, $"Nothing was saved: write {name} as yyyy-MM-ddTHH:mm in the site's time zone, or 'none' to clear it.");
        }

        return (true, await localClock.ToUtcLenientAsync(DateTime.SpecifyKind(local, DateTimeKind.Unspecified)), null);
    }

    // What the coupon admin takes from the product pickers other modules add to its form, set as the Coupon API sets it.
    private static void ApplyLimits(AIFunctionArguments arguments, Coupon coupon)
    {
        if (ReadStrings(arguments, "productIds") is { } productIds)
        {
            coupon.ProductIds = productIds;
        }

        if (ReadStrings(arguments, "categoryIds") is { } categoryIds)
        {
            coupon.CategoryIds = categoryIds;
        }

        if (coupon.Kind != CouponKind.BuyXGetY)
        {
            return;
        }

        if (ReadStrings(arguments, "buyProductIds") is { } buyProductIds)
        {
            coupon.BuyProductIds = buyProductIds;
        }

        if (ReadStrings(arguments, "buyCategoryIds") is { } buyCategoryIds)
        {
            coupon.BuyCategoryIds = buyCategoryIds;
        }
    }

    private static int? Limit(int? given, int? stored)
        => given is { } value ? (value == 0 ? (int?)null : value) : stored;

    private static bool IsNone(string value)
        => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);

    // The editor's field names, as the tool's arguments name them.
    private static string ArgumentName(string key)
        => key switch
        {
            nameof(CouponEditViewModel.MaxRedemptions) => "maxUses",
            nameof(CouponEditViewModel.MaxRedemptionsPerCustomer) => "maxUsesPerCustomer",
            nameof(CouponEditViewModel.EndsUtc) => "ends",
            nameof(CouponEditViewModel.StartsUtc) => "starts",
            nameof(CouponEditViewModel.IsEnabled) => "enabled",
            _ => ToCamel(key),
        };
}
