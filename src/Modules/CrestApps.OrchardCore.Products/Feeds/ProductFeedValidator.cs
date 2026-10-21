using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Google Merchant Center's rules for an item, checked before it goes in a feed: an item that breaks one Merchant Center
/// would refuse is left out, with why.
/// </summary>
public static partial class ProductFeedValidator
{
    public const int MaxIdLength = 50;

    public const int MaxTitleLength = 150;

    public const int MaxDescriptionLength = 5000;

    private static readonly HashSet<string> _availabilities = ["in_stock", "out_of_stock", "backorder", "preorder"];

    private static readonly HashSet<string> _conditions = ["new", "refurbished", "used"];

    /// <summary>
    /// Returns what's wrong with an item, or nothing.
    /// </summary>
    public static IReadOnlyList<string> Validate(ProductFeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > MaxIdLength)
        {
            problems.Add("id is missing or longer than 50 characters");
        }

        if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > MaxTitleLength)
        {
            problems.Add("title is missing or longer than 150 characters");
        }

        if (string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > MaxDescriptionLength)
        {
            problems.Add("description is missing or longer than 5,000 characters");
        }

        if (!IsAbsoluteHttp(item.Link))
        {
            problems.Add("link isn't an absolute web address; set the site's base URL");
        }

        if (!IsAbsoluteHttp(item.ImageLink))
        {
            problems.Add("image_link is missing: give the product a photo");
        }

        if (!_availabilities.Contains(item.Availability ?? string.Empty))
        {
            problems.Add("availability isn't one of in_stock, out_of_stock, backorder or preorder");
        }

        if (item.Availability == "preorder" && item.AvailabilityDateUtc is null)
        {
            problems.Add("a preorder needs availability_date");
        }

        if (item.Price <= 0m || string.IsNullOrEmpty(item.Currency) || item.Currency.Length != 3)
        {
            problems.Add("price is missing, zero or without a currency");
        }

        if (item.SalePrice is { } sale && (sale <= 0m || sale >= item.Price))
        {
            problems.Add("sale_price isn't below price");
        }

        if (!_conditions.Contains(item.Condition ?? string.Empty))
        {
            problems.Add("condition isn't one of new, refurbished or used");
        }

        if (!string.IsNullOrWhiteSpace(item.Gtin) && !IsValidGtin(item.Gtin))
        {
            problems.Add("gtin isn't a valid GTIN-8, 12, 13 or 14");
        }

        return problems;
    }

    /// <summary>
    /// Returns whether a GTIN has 8, 12, 13 or 14 digits and its check digit is right.
    /// </summary>
    public static bool IsValidGtin(string gtin)
    {
        var digits = (gtin ?? string.Empty).Trim();

        if (digits.Length is not (8 or 12 or 13 or 14) || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;

        // From the right, leaving out the check digit, the digits are weighted 3, 1, 3, 1...
        for (var i = digits.Length - 2; i >= 0; i--)
        {
            var weight = (digits.Length - 2 - i) % 2 == 0 ? 3 : 1;
            sum += (digits[i] - '0') * weight;
        }

        return (10 - (sum % 10)) % 10 == digits[^1] - '0';
    }

    private static bool IsAbsoluteHttp(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    /// <summary>
    /// Returns HTML as plain text, its whitespace collapsed, such as a product's description.
    /// </summary>
    public static string ToPlainText(string html)
        => Spaces().Replace(System.Net.WebUtility.HtmlDecode(Tags().Replace(html ?? string.Empty, " ")), " ").Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex("\\s+")]
    private static partial Regex Spaces();
}
