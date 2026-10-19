using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// A code a customer enters to pay less.
/// </summary>
/// <remarks>
/// The two fields that matter most are the ones that stop a coupon costing more than it was meant to. A
/// campaign code that leaks and has no usage limit is unbounded liability, and one with no end date keeps
/// discounting long after the promotion is over. Both are enforced when the coupon is applied, not merely
/// documented on it.
/// </remarks>
public sealed class Coupon : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the code the customer enters. It is matched without case sensitivity, because nobody
    /// types a promotion code the way it was printed.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets whether the buyer enters the code, or the discount applies by itself to every checkout that meets
    /// its conditions. An automatic discount's code is never typed: it only identifies the discount's uses.
    /// </summary>
    public CouponMethod Method { get; set; }

    /// <summary>
    /// Gets a value indicating whether the discount applies by itself, without a code.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAutomatic
        => Method == CouponMethod.Automatic;

    /// <summary>
    /// Gets or sets whether only the unique codes generated for the coupon work, and not its own <see cref="Code"/>.
    /// </summary>
    public bool OnlyGeneratedCodes { get; set; }

    /// <summary>
    /// Gets, when this coupon was looked up by one of its generated codes, the identifier of that code. Its
    /// <see cref="Code"/>, uses and limit are then the generated code's.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string GeneratedCodeId { get; set; }

    /// <summary>
    /// Gets a value indicating whether a buyer can enter the coupon's code: not for an automatic discount, nor for a
    /// coupon whose own code is turned off in favor of the codes generated for it.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CanBeTyped
        => !IsAutomatic && (!OnlyGeneratedCodes || GeneratedCodeId is not null);

    /// <summary>
    /// Gets or sets the description the customer sees on their invoice.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how the reduction is calculated.
    /// </summary>
    public CouponKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the percentage taken off, when <see cref="Kind"/> is
    /// <see cref="CouponKind.Percentage"/>.
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Gets or sets the fixed amount taken off, when <see cref="Kind"/> is
    /// <see cref="CouponKind.FixedAmount"/>.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency a fixed amount is expressed in. A fixed-amount coupon only
    /// applies to an invoice in the same currency, because ten dollars off is not ten euros off.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets what the coupon reduces.
    /// </summary>
    public DiscountTarget Target { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the coupon can be used at all.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the coupon starts being valid.
    /// </summary>
    public DateTime? StartsUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the coupon stops being valid.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// Gets or sets how many times the coupon may be redeemed in total, or <see langword="null"/> for no
    /// limit.
    /// </summary>
    public int? MaxRedemptions { get; set; }

    /// <summary>
    /// Gets or sets how many times it has been redeemed.
    /// </summary>
    public int RedemptionCount { get; set; }

    /// <summary>
    /// Gets or sets the minimum amount the invoice must reach before the coupon applies.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the most the invoice may come to for the coupon to apply, such as a code meant for small orders,
    /// or <see langword="null"/> for no maximum. Like the minimum, it counts the items the coupon applies to.
    /// </summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>
    /// Gets or sets how many times one customer may use the coupon, or <see langword="null"/> for no limit. A
    /// customer is their account, or the email a guest checks out with.
    /// </summary>
    public int? MaxRedemptionsPerCustomer { get; set; }

    /// <summary>
    /// Gets or sets how many of the items it counts the order must have for the coupon to apply, such as "buy 3 or
    /// more", or <see langword="null"/> for no minimum. For a buy X get Y offer, it counts the items bought.
    /// </summary>
    public int? MinimumQuantity { get; set; }

    /// <summary>
    /// Gets or sets, for a buy X get Y offer, how many qualifying items the customer buys for each time the offer
    /// applies.
    /// </summary>
    public int BuyQuantity { get; set; }

    /// <summary>
    /// Gets or sets, for a buy X get Y offer, the products that count toward what the customer buys. With
    /// <see cref="BuyCategoryIds"/> empty too, every item counts.
    /// </summary>
    public IList<string> BuyProductIds { get; set; } = [];

    /// <summary>
    /// Gets or sets, for a buy X get Y offer, the categories whose products count toward what the customer buys.
    /// </summary>
    public IList<string> BuyCategoryIds { get; set; } = [];

    /// <summary>
    /// Gets or sets, for a buy X get Y offer, how many items the customer gets at <see cref="Percentage"/> off (100
    /// for free) each time the offer applies. The items they get are the ones <see cref="ProductIds"/> and
    /// <see cref="CategoryIds"/> allow, or any item when both are empty, the cheapest first.
    /// </summary>
    public int GetQuantity { get; set; }

    /// <summary>
    /// Gets or sets, for a buy X get Y offer, the most times it applies to one order, or <see langword="null"/> for
    /// as many times as the order allows.
    /// </summary>
    public int? MaxApplicationsPerOrder { get; set; }

    /// <summary>
    /// Gets or sets whether the coupon can be used with discounts off some products.
    /// </summary>
    public bool CombinesWithProductDiscounts { get; set; }

    /// <summary>
    /// Gets or sets whether the coupon can be used with discounts off the order.
    /// </summary>
    public bool CombinesWithOrderDiscounts { get; set; }

    /// <summary>
    /// Gets or sets whether the coupon can be used with discounts off shipping.
    /// </summary>
    public bool CombinesWithShippingDiscounts { get; set; }

    /// <summary>
    /// Gets the kind of discount the coupon is: off shipping, off some products, or off the order.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DiscountClass DiscountClass
        => Kind == CouponKind.FreeShipping || Target == DiscountTarget.Shipping
            ? DiscountClass.Shipping
            : HasItemLimits || Kind == CouponKind.BuyXGetY ? DiscountClass.Product : DiscountClass.Order;

    /// <summary>
    /// Gets the kinds of discount the coupon can be used with.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DiscountCombination CombinesWith
        => (CombinesWithProductDiscounts ? DiscountCombination.Product : DiscountCombination.None) |
            (CombinesWithOrderDiscounts ? DiscountCombination.Order : DiscountCombination.None) |
            (CombinesWithShippingDiscounts ? DiscountCombination.Shipping : DiscountCombination.None);

    /// <summary>
    /// Gets or sets the products the coupon is limited to (their content item ids). With
    /// <see cref="CategoryIds"/>, a line qualifies when it is one of these products or in one of those categories.
    /// </summary>
    public IList<string> ProductIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the categories the coupon is limited to (taxonomy term content item ids).
    /// </summary>
    public IList<string> CategoryIds { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the coupon is limited to some products or categories.
    /// </summary>
    public bool HasItemLimits
        => ProductIds?.Count > 0 || CategoryIds?.Count > 0;

    /// <summary>
    /// Gets what the coupon takes something off: shipping for a free-shipping coupon, otherwise its <see cref="Target"/>.
    /// </summary>
    public DiscountTarget EffectiveTarget
        => Kind == CouponKind.FreeShipping ? DiscountTarget.Shipping : Target;

    /// <summary>
    /// Gets or sets the sources the coupon is limited to, such as the source of a transaction (for example a registration fee
    /// source). When it is empty the coupon is not limited by source; otherwise it applies only to a checkout whose
    /// <see cref="CheckoutDiscountScope.Source"/> is one of them, matched without case sensitivity.
    /// </summary>
    public IList<string> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the reference types the coupon is limited to: the kind of thing a checkout pays for
    /// (<see cref="CheckoutSession.ReferenceType"/>, for example <c>Transaction</c>), or what the charge it settles is
    /// for (<see cref="CheckoutDiscountScope.ReferenceType"/>). When it is empty the coupon is not limited by reference
    /// type.
    /// </summary>
    public IList<string> ReferenceTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the one customer who may use the coupon (a user id, or a guest customer id), or
    /// <see langword="null"/> when anyone may. It is matched against the checkout's owner and the customer who owes the
    /// charge it settles.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the email address of the one customer who may use the coupon, or <see langword="null"/> when anyone
    /// may. It is matched without case sensitivity against the email the checkout collected, the email of the customer
    /// who owes the charge it settles, and the email on the buyer's account.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets a value indicating whether the coupon is limited to some sources, reference types or one customer.
    /// </summary>
    public bool IsScoped
        => Sources?.Count > 0 ||
            ReferenceTypes?.Count > 0 ||
            !string.IsNullOrWhiteSpace(CustomerId) ||
            !string.IsNullOrWhiteSpace(CustomerEmail);

    /// <summary>
    /// Gets or sets the UTC time the coupon was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the coupon was last changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Returns whether the coupon may be used right now.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    public bool IsRedeemable(DateTime utcNow)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (StartsUtc.HasValue && utcNow < StartsUtc.Value)
        {
            return false;
        }

        if (EndsUtc.HasValue && utcNow > EndsUtc.Value)
        {
            return false;
        }

        return !MaxRedemptions.HasValue || RedemptionCount < MaxRedemptions.Value;
    }

    /// <summary>
    /// Returns whether an invoice line is one the coupon is for: any line, unless it is limited to some products
    /// or categories.
    /// </summary>
    /// <param name="line">The invoice line.</param>
    public bool AppliesTo(CheckoutLineItem line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return !HasItemLimits || Matches(line, ProductIds, CategoryIds);
    }

    /// <summary>
    /// Returns whether an invoice line counts toward what the customer buys for a buy X get Y offer: any line, unless
    /// the offer names products or categories to buy.
    /// </summary>
    /// <param name="line">The invoice line.</param>
    public bool CountsTowardBuy(CheckoutLineItem line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return (BuyProductIds is not { Count: > 0 } && BuyCategoryIds is not { Count: > 0 }) || Matches(line, BuyProductIds, BuyCategoryIds);
    }

    /// <summary>
    /// Returns what a buy X get Y offer takes off the lines, before rounding, and how much of it comes off each line.
    /// </summary>
    /// <remarks>
    /// Each time the offer applies, the cheapest items the customer can get are discounted, and the dearest of the
    /// items left that count toward what they buy pay for them. So "buy 2 get 1 free" on three shirts makes the
    /// cheapest free, and on six makes the two cheapest free. An item is only ever counted once, either bought or
    /// got.
    /// </remarks>
    /// <param name="lines">The lines the offer can take something off.</param>
    /// <param name="shares">What it takes off each line, by line item id.</param>
    public decimal GetBuyXGetYDiscount(IEnumerable<CheckoutLineItem> lines, out Dictionary<string, decimal> shares)
    {
        shares = [];

        if (Kind != CouponKind.BuyXGetY || BuyQuantity < 1 || GetQuantity < 1 || lines is null)
        {
            return 0m;
        }

        var percentage = Math.Clamp(Percentage, 0m, 100m);

        if (percentage <= 0m)
        {
            return 0m;
        }

        var units = new List<(CheckoutLineItem Line, decimal Price, bool CanBuy, bool CanGet)>();

        foreach (var line in lines.Where(line => line.Quantity > 0 && line.UnitPrice > 0m))
        {
            var canBuy = CountsTowardBuy(line);
            var canGet = AppliesTo(line);

            if (!canBuy && !canGet)
            {
                continue;
            }

            for (var i = 0; i < line.Quantity; i++)
            {
                units.Add((line, line.UnitPrice, canBuy, canGet));
            }
        }

        var used = new bool[units.Count];
        var total = 0m;
        var applications = 0;

        while (!MaxApplicationsPerOrder.HasValue || applications < MaxApplicationsPerOrder.Value)
        {
            var got = Enumerable.Range(0, units.Count)
                .Where(index => !used[index] && units[index].CanGet)
                .OrderBy(index => units[index].Price)
                .Take(GetQuantity)
                .ToList();

            if (got.Count < GetQuantity)
            {
                break;
            }

            var bought = Enumerable.Range(0, units.Count)
                .Where(index => !used[index] && units[index].CanBuy && !got.Contains(index))
                .OrderByDescending(index => units[index].Price)
                .Take(BuyQuantity)
                .ToList();

            if (bought.Count < BuyQuantity)
            {
                break;
            }

            foreach (var index in got.Concat(bought))
            {
                used[index] = true;
            }

            foreach (var index in got)
            {
                var off = units[index].Price * percentage / 100m;
                var itemId = units[index].Line.ItemId ?? string.Empty;

                shares[itemId] = shares.GetValueOrDefault(itemId) + off;
                total += off;
            }

            applications++;
        }

        return total;
    }

    private static bool Matches(CheckoutLineItem line, IList<string> productIds, IList<string> categoryIds)
    {
        if (!string.IsNullOrEmpty(line.ProductId) && productIds?.Contains(line.ProductId, StringComparer.Ordinal) == true)
        {
            return true;
        }

        return line.CategoryIds is { Length: > 0 } categories &&
            categoryIds is { Count: > 0 } &&
            categories.Any(category => categoryIds.Contains(category, StringComparer.Ordinal));
    }

    /// <summary>
    /// Returns whether the amount the coupon counts reaches its minimum and stays within its maximum.
    /// </summary>
    /// <param name="baseAmount">The amount the coupon counts: the items it applies to.</param>
    public bool IsWithinSpend(decimal baseAmount)
    {
        if (baseAmount <= 0m)
        {
            return false;
        }

        if (MinimumAmount.HasValue && baseAmount < MinimumAmount.Value)
        {
            return false;
        }

        return !MaximumAmount.HasValue || baseAmount <= MaximumAmount.Value;
    }

    /// <summary>
    /// Returns the amount this coupon takes off a given base, before rounding. A free-shipping coupon takes nothing
    /// off the items; what it takes off shipping is worked out from the shipping lines.
    /// </summary>
    /// <param name="baseAmount">The amount the coupon applies to.</param>
    /// <param name="currency">The invoice currency.</param>
    public decimal GetDiscount(decimal baseAmount, string currency)
    {
        if (Kind is CouponKind.FreeShipping or CouponKind.BuyXGetY || !IsWithinSpend(baseAmount))
        {
            return 0m;
        }

        if (Kind == CouponKind.Percentage)
        {
            var percentage = Math.Clamp(Percentage, 0m, 100m);

            return baseAmount * percentage / 100m;
        }

        // A fixed amount in another currency is not convertible here, and guessing a rate would charge the
        // customer something nobody chose.
        if (!string.IsNullOrEmpty(Currency) && !string.Equals(Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        return Math.Min(Amount, baseAmount);
    }
}

/// <summary>
/// How a <see cref="Coupon"/> calculates its reduction.
/// </summary>
public enum CouponKind
{
    /// <summary>
    /// A percentage of what the coupon applies to.
    /// </summary>
    Percentage = 0,

    /// <summary>
    /// A fixed amount in a specific currency.
    /// </summary>
    FixedAmount = 1,

    /// <summary>
    /// The shipping charged now, whatever it is.
    /// </summary>
    FreeShipping = 2,

    /// <summary>
    /// A percentage off some items (100 for free) for every so many items bought.
    /// </summary>
    BuyXGetY = 3,
}

/// <summary>
/// How a <see cref="Coupon"/> reaches a checkout.
/// </summary>
public enum CouponMethod
{
    /// <summary>
    /// The buyer enters its code.
    /// </summary>
    Code = 0,

    /// <summary>
    /// It applies by itself to every checkout that meets its conditions.
    /// </summary>
    Automatic = 1,
}
