using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Products.Core.Transfer;

/// <summary>
/// Reads the product CSV files stores move from: Shopify's product export and WooCommerce's. Each becomes the same products, with their options, variants, prices and stock.
/// </summary>
public static class ProductCsv
{
    /// <summary>
    /// The format of Shopify's product export.
    /// </summary>
    public const string Shopify = "Shopify";

    /// <summary>
    /// The format of WooCommerce's product export.
    /// </summary>
    public const string WooCommerce = "WooCommerce";

    /// <summary>
    /// Reads a product export, telling its format from its columns.
    /// </summary>
    public static ProductCsvReadResult Read(string text)
    {
        var (headers, rows) = CsvTable.Parse(text);

        return Read(headers, rows);
    }

    /// <summary>
    /// Reads a product export already split into columns and rows, such as a spreadsheet ContentTransfer opened.
    /// </summary>
    public static ProductCsvReadResult Read(IReadOnlyList<string> headers, IEnumerable<string[]> records)
        => Read(headers, CsvTable.ToRows(headers, records));

    /// <summary>
    /// Returns whether columns are those of a Shopify or WooCommerce product export.
    /// </summary>
    public static bool IsStoreExport(IReadOnlyList<string> headers)
        => (Has(headers, "Handle") && (Has(headers, "Variant Price") || Has(headers, "Title"))) ||
            (Has(headers, "Type") && Has(headers, "Regular price") && Has(headers, "Name"));

    private static ProductCsvReadResult Read(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {

        if (Has(headers, "Handle") && (Has(headers, "Variant Price") || Has(headers, "Title")))
        {
            return ReadShopify(rows);
        }

        if (Has(headers, "Type") && Has(headers, "Regular price") && Has(headers, "Name"))
        {
            return ReadWooCommerce(rows);
        }

        return new ProductCsvReadResult(null, [], ["The file isn't a Shopify or WooCommerce product export: it needs Shopify's Handle and Variant Price columns, or WooCommerce's Type, Name and Regular price."]);
    }

    private static ProductCsvReadResult ReadShopify(IReadOnlyList<CsvRow> rows)
    {
        var products = new List<ImportedProduct>();
        var problems = new List<string>();
        var byHandle = new Dictionary<string, ImportedProduct>(StringComparer.OrdinalIgnoreCase);
        var optionNames = new Dictionary<ImportedProduct, string[]>();

        foreach (var row in rows)
        {
            var handle = row["Handle"];

            if (string.IsNullOrWhiteSpace(handle))
            {
                continue;
            }

            if (!byHandle.TryGetValue(handle, out var product))
            {
                product = new ImportedProduct { Key = handle.Trim(), Row = row.Number };
                byHandle[handle] = product;
                products.Add(product);
            }

            // The product's details are on its first row.
            if (!string.IsNullOrWhiteSpace(row["Title"]) && string.IsNullOrEmpty(product.Title))
            {
                product.Title = row["Title"].Trim();
                product.DescriptionHtml = Blank(row["Body (HTML)"]);
                product.Brand = Blank(row["Vendor"]);
                product.Category = Blank(row["Type"]) ?? LastSegment(row["Product Category"]);
                product.Published = !IsFalse(row["Published"]) && !string.Equals(row["Status"]?.Trim(), "draft", StringComparison.OrdinalIgnoreCase) && !string.Equals(row["Status"]?.Trim(), "archived", StringComparison.OrdinalIgnoreCase);
                optionNames[product] = [.. new[] { row["Option1 Name"], row["Option2 Name"], row["Option3 Name"] }.Select(Blank)];
            }

            AddImage(product, row["Image Src"]);
            AddImage(product, row["Variant Image"]);

            // A row with an image and nothing else adds the image only.
            if (string.IsNullOrWhiteSpace(row["Option1 Value"]) && string.IsNullOrWhiteSpace(row["Variant SKU"]) && string.IsNullOrWhiteSpace(row["Variant Price"]))
            {
                continue;
            }

            var names = optionNames.GetValueOrDefault(product) ?? [];
            var variant = new ImportedVariant
            {
                Sku = Blank(row["Variant SKU"]),
                Price = Decimal(row["Variant Price"]),
                CompareAtPrice = Decimal(row["Variant Compare At Price"]),
                Quantity = Integer(row["Variant Inventory Qty"]),
            };

            var values = new[] { row["Option1 Value"], row["Option2 Value"], row["Option3 Value"] };

            for (var i = 0; i < names.Length; i++)
            {
                // Shopify writes a product without options as one "Title: Default Title" option.
                if (names[i] is { } name && !(string.Equals(name, "Title", StringComparison.OrdinalIgnoreCase) && string.Equals(values[i]?.Trim(), "Default Title", StringComparison.OrdinalIgnoreCase)) && Blank(values[i]) is { } value)
                {
                    variant.Options[name] = value;
                }
            }

            product.Variants.Add(variant);
        }

        foreach (var product in products)
        {
            if (string.IsNullOrEmpty(product.Title))
            {
                problems.Add($"Row {product.Row}: the product '{product.Key}' has no title, so it was left out.");
                continue;
            }

            Settle(product);
        }

        return new ProductCsvReadResult(Shopify, [.. products.Where(product => !string.IsNullOrEmpty(product.Title))], problems);
    }

    private static ProductCsvReadResult ReadWooCommerce(IReadOnlyList<CsvRow> rows)
    {
        var products = new List<ImportedProduct>();
        var problems = new List<string>();
        var byId = new Dictionary<string, ImportedProduct>(StringComparer.OrdinalIgnoreCase);
        var bySku = new Dictionary<string, ImportedProduct>(StringComparer.OrdinalIgnoreCase);
        var variations = new List<CsvRow>();

        foreach (var row in rows)
        {
            var type = row["Type"]?.ToLowerInvariant() ?? string.Empty;

            if (type.Contains("variation", StringComparison.Ordinal))
            {
                variations.Add(row);
                continue;
            }

            if (type.Contains("grouped", StringComparison.Ordinal))
            {
                problems.Add($"Row {row.Number}: '{row["Name"]}' is a grouped product. Its products are imported; group them again with Grouped Products.");
                continue;
            }

            var name = Blank(row["Name"]);

            if (name is null)
            {
                problems.Add($"Row {row.Number}: a product has no name, so it was left out.");
                continue;
            }

            var (price, compareAt) = WooPrices(row);
            var product = new ImportedProduct
            {
                Key = Blank(row["ID"]) ?? Blank(row["SKU"]) ?? Slugify(name),
                Row = row.Number,
                Title = name,
                Sku = Blank(row["SKU"]),
                DescriptionHtml = Blank(row["Description"]) ?? Blank(row["Short description"]),
                Category = FirstCategory(row["Categories"]),
                Brand = FirstCategory(row["Brands"]),
                Published = row["Published"]?.Trim() is not ("0" or "-1"),
                Price = price,
                CompareAtPrice = compareAt,
                Quantity = Integer(row["Stock"]),
                ExternalUrl = type.Contains("external", StringComparison.Ordinal) ? Blank(row["External URL"]) : null,
                ExternalButtonText = type.Contains("external", StringComparison.Ordinal) ? Blank(row["Button text"]) : null,
            };

            foreach (var url in Split(row["Images"]))
            {
                AddImage(product, url);
            }

            // A variable product lists its options and their values.
            if (type.Contains("variable", StringComparison.Ordinal))
            {
                foreach (var (optionName, values) in WooAttributes(row))
                {
                    product.Options.Add(new ProductOption { Name = optionName, Values = [.. Split(values)] });
                }
            }

            products.Add(product);

            if (Blank(row["ID"]) is { } id)
            {
                byId[id] = product;
            }

            if (product.Sku is { } sku)
            {
                bySku[sku] = product;
            }
        }

        foreach (var row in variations)
        {
            // The parent is "id:123" or the parent's SKU.
            var parentKey = row["Parent"]?.Trim() ?? string.Empty;
            var parent = parentKey.StartsWith("id:", StringComparison.OrdinalIgnoreCase)
                ? byId.GetValueOrDefault(parentKey[3..].Trim())
                : bySku.GetValueOrDefault(parentKey) ?? byId.GetValueOrDefault(parentKey);

            if (parent is null)
            {
                problems.Add($"Row {row.Number}: the variation '{row["Name"]}' names a parent ('{parentKey}') that isn't in the file, so it was left out.");
                continue;
            }

            var (price, compareAt) = WooPrices(row);
            var variant = new ImportedVariant
            {
                Sku = Blank(row["SKU"]),
                Price = price ?? parent.Price,
                CompareAtPrice = compareAt,
                Quantity = Integer(row["Stock"]),
            };

            foreach (var (optionName, value) in WooAttributes(row))
            {
                if (Blank(value) is { } chosen)
                {
                    variant.Options[optionName] = chosen;
                }
            }

            parent.Variants.Add(variant);
        }

        foreach (var product in products)
        {
            Settle(product);
        }

        return new ProductCsvReadResult(WooCommerce, products, problems);
    }

    // A product with variants takes its options from them, and its price from the first; a product without takes
    // its price, SKU and stock from its one row.
    private static void Settle(ImportedProduct product)
    {
        var withOptions = product.Variants.Where(variant => variant.Options.Count > 0).ToList();

        if (withOptions.Count == 0)
        {
            if (product.Variants.FirstOrDefault() is { } only)
            {
                product.Sku ??= only.Sku;
                product.Price ??= only.Price;
                product.CompareAtPrice ??= only.CompareAtPrice;
                product.Quantity ??= only.Quantity;
            }

            product.Variants.Clear();
            product.Options.Clear();

            return;
        }

        product.Variants.RemoveAll(variant => variant.Options.Count == 0);

        // The options in the order the file names them, each with the values its variants use.
        var names = product.Options.Select(option => option.Name)
            .Concat(withOptions.SelectMany(variant => variant.Options.Keys))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        product.Options = [.. names.Select(name => new ProductOption
        {
            Name = name,
            Values = [.. (product.Options.FirstOrDefault(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))?.Values ?? [])
                .Concat(withOptions.Select(variant => variant.Options.GetValueOrDefault(name)))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)],
        }).Where(option => option.Values.Count > 0)];

        product.Price ??= withOptions.Select(variant => variant.Price).FirstOrDefault(price => price.HasValue);
    }

    private static (decimal? Price, decimal? CompareAt) WooPrices(CsvRow row)
    {
        var regular = Decimal(row["Regular price"]);
        var sale = Decimal(row["Sale price"]);

        // On sale: it sells at the sale price, and the regular price is what it was.
        return sale.HasValue && regular.HasValue && sale < regular ? (sale, regular) : (sale ?? regular, null);
    }

    private static IEnumerable<(string Name, string Values)> WooAttributes(CsvRow row)
    {
        for (var i = 1; i <= 10; i++)
        {
            if (Blank(row[$"Attribute {i} name"]) is { } name)
            {
                yield return (name, row[$"Attribute {i} value(s)"]);
            }
        }
    }

    private static string FirstCategory(string categories)
        => LastSegment(Split(categories).FirstOrDefault());

    // "Clothing > Shirts" is filed under Shirts.
    private static string LastSegment(string path)
        => Blank(path?.Split('>').LastOrDefault());

    private static string[] Split(string values)
        => (values ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void AddImage(ImportedProduct product, string url)
    {
        if (Blank(url) is { } image && !product.ImageUrls.Contains(image, StringComparer.Ordinal))
        {
            product.ImageUrls.Add(image);
        }
    }

    private static bool Has(IReadOnlyList<string> headers, string name)
        => headers.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool IsFalse(string value)
        => value?.Trim() is { } text && (text.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || text == "0");

    private static string Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? Decimal(string value)
        => decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 0 ? amount : null;

    private static int? Integer(string value)
        => decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? (int)Math.Truncate(amount) : null;

    internal static string Slugify(string text)
    {
        var slug = new StringBuilder();

        foreach (var c in (text ?? string.Empty).Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().Trim('-');
    }
}

/// <summary>
/// What a product export says about a product.
/// </summary>
public sealed class ImportedProduct
{
    /// <summary>
    /// Gets or sets what the file knows the product by: Shopify's handle, WooCommerce's id.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the row the product starts on, for problems.
    /// </summary>
    public int Row { get; set; }

    public string Title { get; set; }

    public string DescriptionHtml { get; set; }

    /// <summary>
    /// Gets or sets the category's name, the last part of a path such as "Clothing > Shirts".
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the brand's name: Shopify's vendor, WooCommerce's brand.
    /// </summary>
    public string Brand { get; set; }

    public string Sku { get; set; }

    public decimal? Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    /// <summary>
    /// Gets or sets how many are in stock, for a product without variants, or <see langword="null"/> when the file
    /// doesn't say.
    /// </summary>
    public int? Quantity { get; set; }

    public bool Published { get; set; } = true;

    public string ExternalUrl { get; set; }

    public string ExternalButtonText { get; set; }

    public List<ProductOption> Options { get; set; } = [];

    public List<ImportedVariant> Variants { get; set; } = [];

    /// <summary>
    /// Gets or sets the addresses of the product's images. They aren't downloaded.
    /// </summary>
    public List<string> ImageUrls { get; set; } = [];
}

/// <summary>
/// What a product export says about one variant of a product.
/// </summary>
public sealed class ImportedVariant
{
    public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Sku { get; set; }

    public decimal? Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public int? Quantity { get; set; }

    /// <summary>
    /// Gets or sets the variant the import made or matched it to.
    /// </summary>
    public string VariantId { get; set; }
}

/// <summary>
/// A product export as read: its format, its products, and what was left out and why.
/// </summary>
public sealed record ProductCsvReadResult(string Format, IReadOnlyList<ImportedProduct> Products, IReadOnlyList<string> Problems);

/// <summary>
/// A row of a CSV file, read by column name. A missing column reads as <see langword="null"/>.
/// </summary>
public sealed class CsvRow
{
    private readonly Dictionary<string, string> _values;

    public CsvRow(int number, Dictionary<string, string> values)
    {
        Number = number;
        _values = values;
    }

    /// <summary>
    /// Gets the row's line in the file, counting the header as 1.
    /// </summary>
    public int Number { get; }

    public string this[string column]
        => _values.GetValueOrDefault(column);
}

/// <summary>
/// Reads and writes CSV: comma-separated, with double quotes around values holding commas, quotes or line breaks.
/// </summary>
public static class CsvTable
{
    /// <summary>
    /// Turns records under the headers into rows read by column name, skipping empty ones. Row numbers count the
    /// header as 1.
    /// </summary>
    public static IReadOnlyList<CsvRow> ToRows(IReadOnlyList<string> headers, IEnumerable<string[]> records)
    {
        var names = (headers ?? []).Select(header => header?.Trim()).ToArray();
        var rows = new List<CsvRow>();
        var number = 1;

        foreach (var record in records ?? [])
        {
            number++;

            if (record is null || record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var c = 0; c < names.Length && c < record.Length; c++)
            {
                if (!string.IsNullOrEmpty(names[c]))
                {
                    values.TryAdd(names[c], record[c]);
                }
            }

            rows.Add(new CsvRow(number, values));
        }

        return rows;
    }

    public static (IReadOnlyList<string> Headers, IReadOnlyList<CsvRow> Rows) Parse(string text)
    {
        var records = ParseRecords(text ?? string.Empty);

        if (records.Count == 0)
        {
            return ([], []);
        }

        var headers = records[0].Select(header => header.Trim()).ToArray();
        var rows = new List<CsvRow>(records.Count - 1);

        for (var i = 1; i < records.Count; i++)
        {
            var record = records[i];

            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var c = 0; c < headers.Length && c < record.Count; c++)
            {
                values.TryAdd(headers[c], record[c]);
            }

            rows.Add(new CsvRow(i + 1, values));
        }

        return (headers, rows);
    }

    public static void AppendRow(StringBuilder csv, IEnumerable<string> values)
        => csv.AppendJoin(',', values.Select(Escape)).Append("\r\n");

    // Quoted when it holds a separator, quote or line break; a cell a spreadsheet would run as a formula starts with
    // an apostrophe instead.
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static List<List<string>> ParseRecords(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;

        for (; i < text.Length; i++)
        {
            var c = text[i];

            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                record.Add(field.ToString());
                field.Clear();
                records.Add(record);
                record = [];
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }
}
