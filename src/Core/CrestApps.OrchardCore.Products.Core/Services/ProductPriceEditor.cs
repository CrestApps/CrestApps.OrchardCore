using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// The rules that decide whether an edited price is coherent, and what it becomes when it is.
/// </summary>
/// <remarks>
/// They live here rather than inside the display driver because they are the part worth testing: a price
/// saved with no interval, a minimum above its maximum, or a second default is a pricing bug that only
/// shows up when somebody tries to buy. The driver is left to do what a driver should — bind a form and
/// render the messages these rules produce.
/// </remarks>
public static class ProductPriceEditor
{
    /// <summary>
    /// Whether an edited row is a real attempt to add a price, as opposed to the blank one the editor
    /// always offers.
    /// </summary>
    /// <param name="amount">The amount typed in, if any.</param>
    /// <param name="name">The name typed in, if any.</param>
    public static bool IsBlank(decimal? amount, string name)
        => !amount.HasValue && string.IsNullOrWhiteSpace(name);

    /// <summary>
    /// Validates one edited price and reports what is wrong with it.
    /// </summary>
    /// <param name="price">The price as the editor submitted it.</param>
    /// <returns>One entry per problem, keyed by the field it belongs to.</returns>
    public static IReadOnlyList<ProductPriceError> Validate(ProductPrice price)
    {
        ArgumentNullException.ThrowIfNull(price);

        var errors = new List<ProductPriceError>();

        if (price.Amount < 0m)
        {
            errors.Add(new ProductPriceError(nameof(ProductPrice.Amount), ProductPriceErrorKind.NegativeAmount));
        }

        if (price.Kind == PriceKind.Recurring)
        {
            // A recurring price with no schedule cannot be sold: there is nothing to tell the gateway about
            // how often to bill it.
            if (price.BillingDuration is null or < 1)
            {
                errors.Add(new ProductPriceError(nameof(ProductPrice.BillingDuration), ProductPriceErrorKind.MissingBillingDuration));
            }

            if (price.Interval is null)
            {
                errors.Add(new ProductPriceError(nameof(ProductPrice.Interval), ProductPriceErrorKind.MissingInterval));
            }
        }

        if (price.AllowCustomAmount &&
            price.MinimumAmount.HasValue &&
            price.MaximumAmount.HasValue &&
            price.MinimumAmount.Value > price.MaximumAmount.Value)
        {
            errors.Add(new ProductPriceError(nameof(ProductPrice.MinimumAmount), ProductPriceErrorKind.MinimumAboveMaximum));
        }

        if (price.TrialDays is < 0)
        {
            errors.Add(new ProductPriceError(nameof(ProductPrice.TrialDays), ProductPriceErrorKind.NegativeTrial));
        }

        if (price.EffectiveFromUtc.HasValue &&
            price.EffectiveToUtc.HasValue &&
            price.EffectiveFromUtc.Value >= price.EffectiveToUtc.Value)
        {
            errors.Add(new ProductPriceError(nameof(ProductPrice.EffectiveToUtc), ProductPriceErrorKind.EndsBeforeItStarts));
        }

        return errors;
    }

    /// <summary>
    /// Settles which price is the default across the whole set.
    /// </summary>
    /// <param name="prices">The prices as the editor submitted them.</param>
    /// <returns>An error when the editor marked more than one, otherwise <see langword="null"/>.</returns>
    /// <remarks>
    /// Exactly one default, always: a link straight to "buy" names no price, so a set with none is
    /// ambiguous and a set with two is a coin toss. When the editor marks none, the first that can be
    /// bought becomes it rather than refusing the whole save over something nobody chose.
    /// </remarks>
    public static ProductPriceError SettleDefault(IList<ProductPrice> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        if (prices.Count == 0)
        {
            return null;
        }

        var defaults = prices.Count(price => price.IsDefault);

        if (defaults > 1)
        {
            return new ProductPriceError(nameof(ProductPricePart.Prices), ProductPriceErrorKind.SeveralDefaults);
        }

        if (defaults == 0)
        {
            prices[0].IsDefault = true;
        }

        return null;
    }
}

/// <summary>
/// One thing wrong with an edited price.
/// </summary>
/// <param name="Field">The field the problem belongs to.</param>
/// <param name="Kind">What is wrong.</param>
public sealed record ProductPriceError(string Field, ProductPriceErrorKind Kind);

/// <summary>
/// What can be wrong with an edited price.
/// </summary>
public enum ProductPriceErrorKind
{
    /// <summary>
    /// The amount is below zero.
    /// </summary>
    NegativeAmount,

    /// <summary>
    /// A recurring price was saved without saying how many interval units make a cycle.
    /// </summary>
    MissingBillingDuration,

    /// <summary>
    /// A recurring price was saved without a billing interval.
    /// </summary>
    MissingInterval,

    /// <summary>
    /// The least the buyer may name is more than the most they may name.
    /// </summary>
    MinimumAboveMaximum,

    /// <summary>
    /// The trial is a negative number of days.
    /// </summary>
    NegativeTrial,

    /// <summary>
    /// The offer ends before it starts.
    /// </summary>
    EndsBeforeItStarts,

    /// <summary>
    /// More than one price was marked as the default.
    /// </summary>
    SeveralDefaults,
}
