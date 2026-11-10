using System.Globalization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Products.Core.Feeds;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// OpenAI's published field rules for a product feed item, checked before it goes in the feed: an item that breaks one
/// OpenAI would reject is left out, with why.
/// </summary>
public static partial class OpenAIProductFeedValidator
{
    private static readonly HashSet<string> _availabilities = new(StringComparer.Ordinal)
    {
        "in_stock", "out_of_stock", "pre_order", "backorder", "unknown",
    };

    private static readonly HashSet<string> _conditions = new(StringComparer.Ordinal)
    {
        "new", "refurbished", "used",
    };

    private static readonly HashSet<string> _weightUnits = new(StringComparer.Ordinal)
    {
        "g", "kg", "oz", "lb",
    };

    private static readonly HashSet<string> _dimensionUnits = new(StringComparer.Ordinal)
    {
        "in", "cm", "ft", "m", "mm",
    };

    /// <summary>
    /// Returns what's wrong with a record, or nothing.
    /// </summary>
    /// <param name="record">The record.</param>
    public static IReadOnlyList<string> Validate(OpenAIProductFeedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var problems = new List<string>();

        Require(problems, "item_id", record.ItemId);
        Require(problems, "title", record.Title);
        Require(problems, "description", record.Description);
        Require(problems, "brand", record.Brand, "set a brand on the product or a default brand under Product feeds");
        Require(problems, "seller_name", record.SellerName, "set the store's name");

        if (!IsAbsoluteHttp(record.Url))
        {
            problems.Add("url isn't an absolute web address; set the site's base URL");
        }

        if (!IsAbsoluteHttp(record.ImageUrl))
        {
            problems.Add("image_url is missing: give the product a photo");
        }

        if (!_availabilities.Contains(record.Availability ?? string.Empty))
        {
            problems.Add("availability isn't one of in_stock, out_of_stock, pre_order, backorder or unknown");
        }

        var price = ParseMoney(record.Price);

        if (price is null)
        {
            problems.Add("price isn't an amount and a currency, such as 79.99 USD");
        }

        if (record.SalePrice is not null)
        {
            var sale = ParseMoney(record.SalePrice);

            if (sale is null || price is null || sale.Value.Currency != price.Value.Currency || sale.Value.Amount >= price.Value.Amount)
            {
                problems.Add("sale_price isn't below price in the same currency");
            }
        }

        if (record.ShippingPrice is not null && (ParseMoney(record.ShippingPrice) is not { } shipping || shipping.Amount < 0m))
        {
            problems.Add("shipping_price isn't an amount and a currency");
        }

        if (!string.IsNullOrEmpty(record.Gtin) && !ProductFeedValidator.IsValidGtin(record.Gtin))
        {
            problems.Add("gtin isn't a valid GTIN-8, 12, 13 or 14");
        }

        if (record.Condition is not null && !_conditions.Contains(record.Condition))
        {
            problems.Add("condition isn't one of new, refurbished or used");
        }

        if (record.ListingHasVariations == true || record.GroupId is not null)
        {
            if (string.IsNullOrEmpty(record.GroupId) || string.Equals(record.GroupId, record.ItemId, StringComparison.Ordinal))
            {
                problems.Add("a variant needs a group_id other than its item_id");
            }

            if (record.VariantDict is not { Count: > 0 })
            {
                problems.Add("a variant needs variant_dict with its options");
            }
        }

        if (record.Weight is not null && !_weightUnits.Contains(record.ItemWeightUnit ?? string.Empty))
        {
            problems.Add("weight needs item_weight_unit: g, kg, oz or lb");
        }

        if ((record.Length is not null || record.Width is not null || record.Height is not null) && !_dimensionUnits.Contains(record.DimensionsUnit ?? string.Empty))
        {
            problems.Add("length, width and height need dimensions_unit: in or cm");
        }

        if (record.IsEligibleCheckout == true)
        {
            if (!IsAbsoluteHttp(record.SellerPrivacyPolicy))
            {
                problems.Add("checkout needs seller_privacy_policy: link the privacy policy in the store profile");
            }

            if (!IsAbsoluteHttp(record.SellerTos))
            {
                problems.Add("checkout needs seller_tos: link the terms of service in the store profile");
            }
        }

        foreach (var (name, url) in new[] { ("seller_url", record.SellerUrl), ("return_policy", record.ReturnPolicy) })
        {
            if (url is not null && !IsAbsoluteHttp(url))
            {
                problems.Add($"{name} isn't an absolute web address");
            }
        }

        return problems;
    }

    /// <summary>
    /// Reads money as OpenAI writes it, such as <c>79.99 USD</c>: a decimal amount without thousands separators, a
    /// space and an uppercase ISO 4217 code. Returns <see langword="null"/> for anything else.
    /// </summary>
    /// <param name="value">The money.</param>
    public static (decimal Amount, string Currency)? ParseMoney(string value)
    {
        var match = MoneyPattern().Match(value ?? string.Empty);

        return match.Success && decimal.TryParse(match.Groups["amount"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? (amount, match.Groups["currency"].Value)
            : null;
    }

    private static void Require(List<string> problems, string name, string value, string fix = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add(fix is null ? $"{name} is missing" : $"{name} is missing: {fix}");
        }
    }

    private static bool IsAbsoluteHttp(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    [GeneratedRegex(@"^(?<amount>\d+(\.\d+)?) (?<currency>[A-Z]{3})$")]
    private static partial Regex MoneyPattern();
}
