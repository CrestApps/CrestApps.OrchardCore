using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Products.Core.Listing;

namespace CrestApps.OrchardCore.Products.Core.Search;

/// <summary>
/// The fields a product search record is written as in a search server's index.
/// </summary>
/// <remarks>
/// Every provider writes the same fields, so the query side reads the same names whichever server holds them. The
/// names keep to letters, digits and underscores, starting with a letter, which every provider accepts (Azure AI
/// Search is the strictest).
/// <para>
/// Each attribute becomes a handful of fields under a name made from its key: its values, for filters; its facet
/// tokens, which carry the level, label and order next to the value, so a count comes back ready to show; its
/// numbers, for ranges; and a number and a text to sort by.
/// </para>
/// </remarks>
public static class ProductSearchIndexFields
{
    /// <summary>
    /// The index type of a product search index in Orchard Core Indexing.
    /// </summary>
    public const string IndexingTaskType = "ProductSearch";

    public const string ContentItemId = "ContentItemId";

    public const string ContentType = "ContentType";

    /// <summary>
    /// The product's culture, or <see cref="NeutralCulture"/> for a product that is not localized, which shows in
    /// every culture.
    /// </summary>
    public const string Culture = "Culture";

    public const string NeutralCulture = "neutral";

    /// <summary>
    /// The title, analyzed for keyword search.
    /// </summary>
    public const string Title = "Title";

    /// <summary>
    /// The title in lower case, for sorting A to Z.
    /// </summary>
    public const string TitleSort = "TitleSort";

    public const string Sku = "Sku";

    /// <summary>
    /// Everything a keyword search matches: the title, the SKU and the searchable attribute values.
    /// </summary>
    public const string Keywords = "Keywords";

    public const string Price = "Price";

    /// <summary>
    /// 1 for a featured product, 0 otherwise, so it sorts.
    /// </summary>
    public const string Featured = "Featured";

    public const string Position = "Position";

    public const string PublishedUtc = "PublishedUtc";

    /// <summary>
    /// The record's number in the database, the last tie-break, so pages never overlap.
    /// </summary>
    public const string Sequence = "Sequence";

    public const string ValuesSuffix = "_v";

    public const string FacetsSuffix = "_f";

    public const string NumbersSuffix = "_n";

    public const string SortNumberSuffix = "_sn";

    public const string MaxNumberSuffix = "_nx";

    public const string SortTextSuffix = "_st";

    /// <summary>
    /// Separates the parts of a facet token. A unit separator, which no value or label contains.
    /// </summary>
    public const char TokenSeparator = '\u001f';

    /// <summary>
    /// Longer values (a description written as rich text) are searched, but not filtered, counted or sorted on.
    /// </summary>
    public const int MaxKeywordLength = 256;

    /// <summary>
    /// The base name of an attribute's fields, such as <c>a_vehicle_year_1a2b3c4d</c>. The hash keeps keys that
    /// differ only in punctuation apart.
    /// </summary>
    public static string Attribute(string attributeKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(attributeKey);

        var slug = new StringBuilder("a_");

        foreach (var character in attributeKey.ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug.Append(character);
            }
            else if (slug[^1] != '_')
            {
                slug.Append('_');
            }

            if (slug.Length >= 48)
            {
                break;
            }
        }

        if (slug[^1] != '_')
        {
            slug.Append('_');
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(attributeKey)), 0, 4);

        return slug.Append(hash).ToString();
    }

    public static string Values(string attributeKey)
        => Attribute(attributeKey) + ValuesSuffix;

    public static string Facets(string attributeKey)
        => Attribute(attributeKey) + FacetsSuffix;

    public static string Numbers(string attributeKey)
        => Attribute(attributeKey) + NumbersSuffix;

    public static string SortNumber(string attributeKey)
        => Attribute(attributeKey) + SortNumberSuffix;

    public static string SortText(string attributeKey)
        => Attribute(attributeKey) + SortTextSuffix;

    /// <summary>
    /// An attribute's largest number. With <see cref="SortNumber"/>, the smallest, it gives a provider that cannot
    /// hold a list of numbers its ranges and bounds.
    /// </summary>
    public static string MaxNumber(string attributeKey)
        => Attribute(attributeKey) + MaxNumberSuffix;

    /// <summary>
    /// The facet token of a value: its level, value, label and order.
    /// </summary>
    public static string ToFacetToken(ProductAttributeValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return string.Join(
            TokenSeparator,
            value.Level.ToString(CultureInfo.InvariantCulture),
            value.Text,
            string.IsNullOrEmpty(value.Label) ? value.Text : value.Label,
            value.Order.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Reads a facet token back. Returns false for a token that was not written by <see cref="ToFacetToken"/>.
    /// </summary>
    public static bool TryParseFacetToken(string token, out int level, out ProductFacetValue value)
    {
        level = 0;
        value = null;

        var parts = token?.Split(TokenSeparator);

        if (parts is null || parts.Length != 4 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out level) ||
            !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var order))
        {
            return false;
        }

        value = new ProductFacetValue
        {
            Value = parts[1],
            Label = string.IsNullOrEmpty(parts[2]) ? parts[1] : parts[2],
            Order = order,
        };

        return true;
    }

    /// <summary>
    /// Turns the counts of an attribute's facet tokens into a filter's options, keeping the requested level only.
    /// </summary>
    public static ProductFacetResult ToFacetResult(ProductFacetRequest facet, IEnumerable<KeyValuePair<string, long>> tokenCounts)
    {
        ArgumentNullException.ThrowIfNull(facet);

        var values = new Dictionary<string, ProductFacetValue>(StringComparer.Ordinal);

        foreach (var (token, count) in tokenCounts ?? [])
        {
            if (!TryParseFacetToken(token, out var level, out var value) ||
                (facet.Level.HasValue && level != facet.Level.Value))
            {
                continue;
            }

            // The same value with another label (a term renamed since) counts once.
            if (values.TryGetValue(value.Value, out var existing))
            {
                existing.Count += (int)Math.Min(count, int.MaxValue);
                continue;
            }

            value.Count = (int)Math.Min(count, int.MaxValue);
            values[value.Value] = value;
        }

        return new ProductFacetResult
        {
            Key = facet.Key,
            Values = values.Values.ToArray(),
        };
    }

    /// <summary>
    /// The fields of one product's record, as a provider writes them.
    /// </summary>
    public static IReadOnlyList<ProductIndexField> GetFields(ProductSearchDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var fields = new List<ProductIndexField>
        {
            ProductIndexField.Keyword(ContentItemId, document.ContentItemId),
            ProductIndexField.Keyword(ContentType, document.ContentType),
            ProductIndexField.Keyword(Culture, string.IsNullOrEmpty(document.Culture) ? NeutralCulture : document.Culture),
            ProductIndexField.Text(Title, document.Title),
            ProductIndexField.Keyword(TitleSort, document.Title?.ToLowerInvariant()),
            ProductIndexField.Keyword(Sku, document.Sku),
            ProductIndexField.Number(Price, document.Price),
            ProductIndexField.Number(Featured, document.Featured ? 1 : 0),
            ProductIndexField.Number(Position, document.Position),
            ProductIndexField.Date(PublishedUtc, document.PublishedUtc),
            ProductIndexField.Number(Sequence, document.Id),
        };

        var keywords = new List<string> { document.Title, document.Sku };

        foreach (var group in (document.Attributes ?? []).Where(value => !string.IsNullOrEmpty(value?.Key)).GroupBy(value => value.Key, StringComparer.Ordinal))
        {
            var values = new List<string>();
            var tokens = new List<string>();
            var numbers = new List<decimal>();
            string sortText = null;

            foreach (var value in group)
            {
                if (value.Searchable)
                {
                    keywords.Add(value.Label);

                    if (!string.Equals(value.Label, value.Text, StringComparison.Ordinal))
                    {
                        keywords.Add(value.Text);
                    }
                }

                if (value.Number.HasValue)
                {
                    numbers.Add(value.Number.Value);
                }

                if (string.IsNullOrEmpty(value.Text) || value.Text.Length > MaxKeywordLength)
                {
                    continue;
                }

                values.Add(value.Text);
                tokens.Add(ToFacetToken(value));

                var text = (string.IsNullOrEmpty(value.Label) ? value.Text : value.Label).ToLowerInvariant();

                if (sortText is null || string.CompareOrdinal(text, sortText) < 0)
                {
                    sortText = text;
                }
            }

            var key = group.Key;

            if (values.Count > 0)
            {
                fields.Add(new ProductIndexField(Values(key), ProductIndexFieldKind.Keyword, values.Distinct(StringComparer.Ordinal).Cast<object>().ToArray()));
                fields.Add(new ProductIndexField(Facets(key), ProductIndexFieldKind.Keyword, tokens.Distinct(StringComparer.Ordinal).Cast<object>().ToArray()));
                fields.Add(ProductIndexField.Keyword(SortText(key), sortText));
            }

            if (numbers.Count > 0)
            {
                fields.Add(new ProductIndexField(Numbers(key), ProductIndexFieldKind.Number, numbers.Distinct().Cast<object>().ToArray()));
                fields.Add(ProductIndexField.Number(SortNumber(key), numbers.Min()));
                fields.Add(ProductIndexField.Number(MaxNumber(key), numbers.Max()));
            }
        }

        fields.Add(ProductIndexField.Text(Keywords, string.Join(' ', keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Distinct(StringComparer.Ordinal))));

        return fields.Where(field => field.Values.Count > 0).ToArray();
    }

    /// <summary>
    /// The fields an index needs for the given attribute keys, for a provider that declares every field up front.
    /// </summary>
    /// <remarks>
    /// Includes the built-in attributes every product has (the price, the type, whether it is featured or can be
    /// bought), whether or not they are passed.
    /// </remarks>
    public static IReadOnlyList<ProductIndexFieldDefinition> GetDefinitions(IEnumerable<string> attributeKeys)
    {
        var definitions = new List<ProductIndexFieldDefinition>
        {
            new(ContentItemId, ProductIndexFieldKind.Keyword) { IsKey = true },
            new(ContentType, ProductIndexFieldKind.Keyword),
            new(Culture, ProductIndexFieldKind.Keyword),
            new(Title, ProductIndexFieldKind.Text),
            new(TitleSort, ProductIndexFieldKind.Keyword) { IsSortable = true },
            new(Sku, ProductIndexFieldKind.Keyword),
            new(Keywords, ProductIndexFieldKind.Text),
            new(Price, ProductIndexFieldKind.Number) { IsSortable = true },
            new(Featured, ProductIndexFieldKind.Number) { IsSortable = true },
            new(Position, ProductIndexFieldKind.Number) { IsSortable = true },
            new(PublishedUtc, ProductIndexFieldKind.Date) { IsSortable = true },
            new(Sequence, ProductIndexFieldKind.Number) { IsSortable = true },
        };

        string[] builtIn = [ProductAttributeKeys.Price, ProductAttributeKeys.ContentType, ProductAttributeKeys.Purchasable, ProductAttributeKeys.Featured, ProductAttributeKeys.InStock, ProductAttributeKeys.Rating];

        foreach (var key in builtIn.Concat(attributeKeys ?? []).Where(key => !string.IsNullOrEmpty(key)).Distinct(StringComparer.Ordinal))
        {
            definitions.Add(new(Values(key), ProductIndexFieldKind.Keyword) { IsCollection = true });
            definitions.Add(new(Facets(key), ProductIndexFieldKind.Keyword) { IsCollection = true, IsFacetable = true });
            definitions.Add(new(Numbers(key), ProductIndexFieldKind.Number) { IsCollection = true, IsFacetable = true });
            definitions.Add(new(SortNumber(key), ProductIndexFieldKind.Number) { IsSortable = true });
            definitions.Add(new(MaxNumber(key), ProductIndexFieldKind.Number) { IsSortable = true });
            definitions.Add(new(SortText(key), ProductIndexFieldKind.Keyword) { IsSortable = true });
        }

        return definitions;
    }
}

/// <summary>
/// The kind of value an index field holds.
/// </summary>
public enum ProductIndexFieldKind
{
    /// <summary>
    /// Matched exactly: filtered on, counted and sorted on.
    /// </summary>
    Keyword,

    /// <summary>
    /// Analyzed for keyword search.
    /// </summary>
    Text,

    Number,

    Date,
}

/// <summary>
/// One field of a product's record in a search server's index, with one value or several.
/// </summary>
public sealed class ProductIndexField
{
    public ProductIndexField(string name, ProductIndexFieldKind kind, IReadOnlyList<object> values)
    {
        Name = name;
        Kind = kind;
        Values = values ?? [];
    }

    public string Name { get; }

    public ProductIndexFieldKind Kind { get; }

    public IReadOnlyList<object> Values { get; }

    public static ProductIndexField Keyword(string name, string value)
        => new(name, ProductIndexFieldKind.Keyword, string.IsNullOrEmpty(value) ? [] : [value]);

    public static ProductIndexField Text(string name, string value)
        => new(name, ProductIndexFieldKind.Text, string.IsNullOrWhiteSpace(value) ? [] : [value]);

    public static ProductIndexField Number(string name, decimal? value)
        => new(name, ProductIndexFieldKind.Number, value.HasValue ? [value.Value] : []);

    public static ProductIndexField Date(string name, DateTime? value)
        => new(name, ProductIndexFieldKind.Date, value.HasValue ? [value.Value] : []);
}

/// <summary>
/// A field a product search index declares, for a provider that needs every field up front.
/// </summary>
public sealed class ProductIndexFieldDefinition
{
    public ProductIndexFieldDefinition(string name, ProductIndexFieldKind kind)
    {
        Name = name;
        Kind = kind;
    }

    public string Name { get; }

    public ProductIndexFieldKind Kind { get; }

    public bool IsKey { get; init; }

    public bool IsCollection { get; init; }

    public bool IsFacetable { get; init; }

    public bool IsSortable { get; init; }
}
