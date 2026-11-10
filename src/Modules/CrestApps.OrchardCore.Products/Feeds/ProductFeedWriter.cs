using System.Globalization;
using System.Text;
using System.Xml;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Tabular;

namespace CrestApps.OrchardCore.Products.Feeds;

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
            "identifier_exists", "condition", "google_product_category", "product_type", "color", "size", "shipping_weight",
            "shipping_length", "shipping_width", "shipping_height",
        };

        var csv = new StringBuilder();

        CsvWriter.AppendRow(csv, columns);

        foreach (var item in feed.Items)
        {
            var values = Attributes(item)
                .GroupBy(attribute => attribute.Name)
                .ToDictionary(group => group.Key, group => string.Join(',', group.Select(attribute => attribute.Value)));

            csv.AppendJoin(',', columns.Select(column => Quote(values.GetValueOrDefault(column)))).Append(CsvWriter.RecordSeparator);
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

        // Google reads a weight in lb, oz, g or kg, and a size in in or cm.
        if (item.Weight is > 0m && item.WeightUnit is "lb" or "oz" or "g" or "kg")
        {
            yield return ("shipping_weight", Measure(item.Weight.Value, item.WeightUnit));
        }

        if (item is { Length: > 0m, Width: > 0m, Height: > 0m } && item.DimensionsUnit is "in" or "cm")
        {
            yield return ("shipping_length", Measure(item.Length.Value, item.DimensionsUnit));
            yield return ("shipping_width", Measure(item.Width.Value, item.DimensionsUnit));
            yield return ("shipping_height", Measure(item.Height.Value, item.DimensionsUnit));
        }
    }

    private static string Measure(decimal value, string unit)
        => value.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit;

    private static string Quote(string value)
        => CsvWriter.Escape(value?.Replace('\n', ' ').Replace('\r', ' '), guardFormula: false);
}
