using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// What a product feed says about a product, or about one of its variants: Google Merchant Center's attributes, which
/// Meta and TikTok catalogs read too.
/// </summary>
public sealed class ProductFeedItem
{
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the product a variant belongs to, or none for a product without variants.
    /// </summary>
    public string ItemGroupId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Link { get; set; }

    public string ImageLink { get; set; }

    public List<string> AdditionalImageLinks { get; set; } = [];

    /// <summary>
    /// Gets or sets <c>in_stock</c>, <c>out_of_stock</c>, <c>backorder</c> or <c>preorder</c>.
    /// </summary>
    public string Availability { get; set; }

    public DateTime? AvailabilityDateUtc { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; }

    public decimal? SalePrice { get; set; }

    public DateTime? SaleStartsUtc { get; set; }

    public DateTime? SaleEndsUtc { get; set; }

    public string Brand { get; set; }

    public string Gtin { get; set; }

    public string Mpn { get; set; }

    /// <summary>
    /// Gets or sets <c>new</c>, <c>refurbished</c> or <c>used</c>.
    /// </summary>
    public string Condition { get; set; } = "new";

    public string GoogleProductCategory { get; set; }

    public string ProductType { get; set; }

    public string Color { get; set; }

    public string Size { get; set; }

    /// <summary>
    /// Gets whether the product has a brand and a GTIN or MPN; when it doesn't, the feed says so with
    /// <c>identifier_exists</c>.
    /// </summary>
    public bool IdentifierExists
        => !string.IsNullOrWhiteSpace(Brand) && (!string.IsNullOrWhiteSpace(Gtin) || !string.IsNullOrWhiteSpace(Mpn));
}

/// <summary>
/// A feed and why the products left out of it were.
/// </summary>
public sealed record ProductFeed(IReadOnlyList<ProductFeedItem> Items, IReadOnlyList<ProductFeedProblem> Problems, DateTime BuiltUtc);

/// <summary>
/// Why an item would be refused, or was left out of the feed.
/// </summary>
public sealed record ProductFeedProblem(string ItemId, string Title, string Problem, bool LeftOut);

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

/// <summary>
/// Writes a feed as Google Merchant Center's RSS 2.0 with the <c>g:</c> attributes, or as a CSV with the same columns.
/// </summary>
public static class ProductFeedWriter
{
    private const string GoogleNamespace = "http://base.google.com/ns/1.0";

    public static string ToXml(ProductFeed feed, string title, string link)
    {
        ArgumentNullException.ThrowIfNull(feed);

        var output = new StringBuilder();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("rss");
            writer.WriteAttributeString("version", "2.0");
            writer.WriteAttributeString("xmlns", "g", null, GoogleNamespace);
            writer.WriteStartElement("channel");
            writer.WriteElementString("title", title);
            writer.WriteElementString("link", link);
            writer.WriteElementString("description", title);

            foreach (var item in feed.Items)
            {
                writer.WriteStartElement("item");

                foreach (var (name, value) in Attributes(item))
                {
                    writer.WriteElementString("g", name, GoogleNamespace, value);
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        // StringBuilder output is UTF-16; the declaration says what's served.
        return output.ToString().Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"", StringComparison.Ordinal);
    }

    public static string ToCsv(ProductFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);

        var columns = new[]
        {
            "id", "item_group_id", "title", "description", "link", "image_link", "additional_image_link", "availability",
            "availability_date", "price", "sale_price", "sale_price_effective_date", "brand", "gtin", "mpn",
            "identifier_exists", "condition", "google_product_category", "product_type", "color", "size",
        };

        var csv = new StringBuilder().AppendLine(string.Join(',', columns));

        foreach (var item in feed.Items)
        {
            var values = Attributes(item)
                .GroupBy(attribute => attribute.Name)
                .ToDictionary(group => group.Key, group => string.Join(',', group.Select(attribute => attribute.Value)));

            csv.AppendLine(string.Join(',', columns.Select(column => Quote(values.GetValueOrDefault(column)))));
        }

        return csv.ToString();
    }

    internal static IEnumerable<(string Name, string Value)> Attributes(ProductFeedItem item)
    {
        static string Money(decimal amount, string currency)
            => amount.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency;

        static string Date(DateTime utc)
            => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture);

        yield return ("id", item.Id);

        if (!string.IsNullOrEmpty(item.ItemGroupId))
        {
            yield return ("item_group_id", item.ItemGroupId);
        }

        yield return ("title", item.Title);
        yield return ("description", item.Description);
        yield return ("link", item.Link);
        yield return ("image_link", item.ImageLink);

        foreach (var image in item.AdditionalImageLinks.Take(10))
        {
            yield return ("additional_image_link", image);
        }

        yield return ("availability", item.Availability);

        if (item.AvailabilityDateUtc is { } available)
        {
            yield return ("availability_date", Date(available));
        }

        yield return ("price", Money(item.Price, item.Currency));

        if (item.SalePrice is { } sale)
        {
            yield return ("sale_price", Money(sale, item.Currency));

            if (item.SaleStartsUtc is { } starts && item.SaleEndsUtc is { } ends)
            {
                yield return ("sale_price_effective_date", Date(starts) + "/" + Date(ends));
            }
        }

        if (!string.IsNullOrWhiteSpace(item.Brand))
        {
            yield return ("brand", item.Brand);
        }

        if (!string.IsNullOrWhiteSpace(item.Gtin))
        {
            yield return ("gtin", item.Gtin.Trim());
        }

        if (!string.IsNullOrWhiteSpace(item.Mpn))
        {
            yield return ("mpn", item.Mpn.Trim());
        }

        if (!item.IdentifierExists)
        {
            yield return ("identifier_exists", "no");
        }

        yield return ("condition", item.Condition);

        if (!string.IsNullOrWhiteSpace(item.GoogleProductCategory))
        {
            yield return ("google_product_category", item.GoogleProductCategory);
        }

        if (!string.IsNullOrWhiteSpace(item.ProductType))
        {
            yield return ("product_type", item.ProductType);
        }

        if (!string.IsNullOrWhiteSpace(item.Color))
        {
            yield return ("color", item.Color);
        }

        if (!string.IsNullOrWhiteSpace(item.Size))
        {
            yield return ("size", item.Size);
        }
    }

    private static string Quote(string value)
        => string.IsNullOrEmpty(value) ? string.Empty : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ') + "\"";
}

/// <summary>
/// What a product says to feeds that the product itself doesn't: its brand and identifiers, its condition, its Google
/// category, or that it stays out of them.
/// </summary>
public sealed class ProductFeedPart : ContentPart
{
    public string Brand { get; set; }

    /// <summary>
    /// Gets or sets the GTIN (UPC, EAN, ISBN) of the product, or of each variant by its id.
    /// </summary>
    public string Gtin { get; set; }

    public Dictionary<string, string> VariantGtins { get; set; } = [];

    public string Mpn { get; set; }

    public string Condition { get; set; }

    /// <summary>
    /// Gets or sets the product's Google product category, by name or number, such as <c>Apparel &amp; Accessories &gt; Clothing</c>.
    /// </summary>
    public string GoogleProductCategory { get; set; }

    public bool ExcludeFromFeeds { get; set; }
}

/// <summary>
/// Settings › Commerce › Product feeds.
/// </summary>
public sealed class ProductFeedSettings
{
    /// <summary>
    /// Gets or sets the brand of products that name none, such as the store's own.
    /// </summary>
    public string DefaultBrand { get; set; }

    public string DefaultCondition { get; set; } = "new";

    public string DefaultGoogleProductCategory { get; set; }

    /// <summary>
    /// Gets or sets whether products that are out of stock stay in the feed, marked so.
    /// </summary>
    public bool IncludeOutOfStock { get; set; } = true;

    /// <summary>
    /// Gets or sets a key the feed's address has to carry, or none for a public feed.
    /// </summary>
    public string Key { get; set; }
}
