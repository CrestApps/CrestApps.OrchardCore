using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Tabular;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Writes the store's product feed in OpenAI's format for ChatGPT: each item as OpenAI's fields, as JSON lines or as
/// CSV, gzipped or not, and split into shards for an SFTP push.
/// </summary>
public static class OpenAIProductFeedWriter
{
    /// <summary>
    /// The most items one shard file holds, as OpenAI recommends.
    /// </summary>
    public const int MaxItemsPerShard = 500_000;

    /// <summary>
    /// The CSV columns, in order.
    /// </summary>
    public static readonly IReadOnlyList<string> Columns =
    [
        "item_id", "title", "description", "url", "brand", "seller_name", "image_url", "additional_image_urls",
        "availability", "price", "sale_price", "group_id", "listing_has_variations", "variant_dict", "gtin", "mpn",
        "condition", "product_category", "color", "size", "weight", "item_weight_unit", "length", "width", "height",
        "dimensions_unit", "shipping_price", "accepts_returns", "return_deadline_in_days", "return_policy",
        "accepts_exchanges", "seller_url", "seller_privacy_policy", "seller_tos", "is_digital", "is_eligible_search",
        "is_eligible_checkout", "store_country", "target_countries",
    ];

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Returns a product feed item as OpenAI's fields.
    /// </summary>
    /// <param name="item">The product feed item.</param>
    /// <param name="context">What every item says about the store.</param>
    public static OpenAIProductFeedRecord ToRecord(ProductFeedItem item, OpenAIProductFeedContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        var isVariant = !string.IsNullOrEmpty(item.ItemGroupId);
        var hasDimensions = item.Length is not null && item.Width is not null && item.Height is not null;

        return new OpenAIProductFeedRecord
        {
            ItemId = Text(item.Id),
            Title = Text(item.Title),
            Description = Text(item.Description),
            Url = Text(item.Link),
            Brand = Text(item.Brand),
            SellerName = Text(item.SellerName),
            ImageUrl = Text(item.ImageLink),
            AdditionalImageUrls = List(item.AdditionalImageLinks),
            Availability = item.Availability switch
            {
                "in_stock" => "in_stock",
                "out_of_stock" => "out_of_stock",
                "preorder" => "pre_order",
                "backorder" => "backorder",
                _ => "unknown",
            },
            Price = string.IsNullOrEmpty(item.Currency) ? null : Money(item.Price, item.Currency),
            SalePrice = item.SalePrice is { } sale && !string.IsNullOrEmpty(item.Currency) ? Money(sale, item.Currency) : null,
            GroupId = isVariant ? item.ItemGroupId : null,
            ListingHasVariations = isVariant,
            VariantDict = isVariant && item.VariantOptions is { Count: > 0 } options
                ? new Dictionary<string, string>(options.Where(option => !string.IsNullOrWhiteSpace(option.Value)), StringComparer.OrdinalIgnoreCase)
                : null,
            Gtin = Text(item.Gtin),
            Mpn = Text(item.Mpn),
            Condition = Text(item.Condition),
            ProductCategory = Text(item.ProductType) ?? (item.GoogleProductCategory is { Length: > 0 } category && !category.All(char.IsAsciiDigit) ? category : null),
            Color = Text(item.Color),
            Size = Text(item.Size),
            Weight = item.Weight is > 0m ? item.Weight : null,
            ItemWeightUnit = item.Weight is > 0m ? Text(item.WeightUnit) : null,
            Length = hasDimensions ? item.Length : null,
            Width = hasDimensions ? item.Width : null,
            Height = hasDimensions ? item.Height : null,
            DimensionsUnit = hasDimensions ? Text(item.DimensionsUnit) : null,
            ShippingPrice = item.ShippingPrice is { } shipping && !string.IsNullOrEmpty(item.Currency) ? Money(shipping, item.Currency) : null,
            AcceptsReturns = item.AcceptsReturns,
            ReturnDeadlineInDays = item.AcceptsReturns == true ? item.ReturnWindowDays : null,
            ReturnPolicy = Text(item.ReturnPolicyUrl),
            AcceptsExchanges = item.AcceptsExchanges,
            SellerUrl = Text(item.SellerUrl),
            SellerPrivacyPolicy = Text(item.PrivacyPolicyUrl),
            SellerTos = Text(item.TermsOfServiceUrl),
            IsDigital = item.IsDigital,
            IsEligibleSearch = item.ShowToAgents,
            IsEligibleCheckout = item.ShowToAgents && item.AgentCheckout,
            StoreCountry = Text(context.StoreCountry)?.ToUpperInvariant(),
            TargetCountries = List(context.TargetCountries?.Select(country => country?.Trim().ToUpperInvariant())),
        };
    }

    /// <summary>
    /// Returns an amount as OpenAI writes money: the amount at its currency's precision, a space and the uppercase
    /// currency code, such as <c>79.99 USD</c>.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The ISO 4217 code of its currency.</param>
    public static string Money(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrEmpty(currency);

        return CurrencyScale.Format(CurrencyScale.Round(amount, currency), currency) + " " + currency.ToUpperInvariant();
    }

    /// <summary>
    /// Writes records in a format to a stream: JSON lines or CSV, gzipped when the format says so.
    /// </summary>
    /// <param name="records">The records.</param>
    /// <param name="format">One of <see cref="OpenAIProductFeedFormats"/>.</param>
    /// <param name="output">The stream written to; it stays open.</param>
    public static void Write(IEnumerable<OpenAIProductFeedRecord> records, string format, Stream output)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(output);

        if (!OpenAIProductFeedFormats.IsValid(format))
        {
            throw new ArgumentException($"'{format}' isn't a format of the OpenAI product feed.", nameof(format));
        }

        if (OpenAIProductFeedFormats.IsGzip(format))
        {
            using var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);

            WriteText(records, format, gzip);

            return;
        }

        WriteText(records, format, output);
    }

    /// <summary>
    /// Returns records as a file in a format.
    /// </summary>
    /// <param name="records">The records.</param>
    /// <param name="format">One of <see cref="OpenAIProductFeedFormats"/>.</param>
    public static byte[] ToBytes(IEnumerable<OpenAIProductFeedRecord> records, string format)
    {
        using var output = new MemoryStream();

        Write(records, format, output);

        return output.ToArray();
    }

    /// <summary>
    /// Returns one JSON line: the record's fields that have a value.
    /// </summary>
    /// <param name="record">The record.</param>
    public static string ToJsonLine(OpenAIProductFeedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return JsonSerializer.Serialize(record, _jsonOptions);
    }

    /// <summary>
    /// Returns the cells of one CSV row, in the order of <see cref="Columns"/>, before escaping: booleans in lowercase,
    /// lists joined by commas, and a variant's options as a JSON object.
    /// </summary>
    /// <param name="record">The record.</param>
    public static IReadOnlyList<string> ToCsvCells(OpenAIProductFeedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return
        [
            record.ItemId, record.Title, record.Description, record.Url, record.Brand, record.SellerName, record.ImageUrl,
            Join(record.AdditionalImageUrls), record.Availability, record.Price, record.SalePrice, record.GroupId,
            Boolean(record.ListingHasVariations), record.VariantDict is null ? null : JsonSerializer.Serialize(record.VariantDict),
            record.Gtin, record.Mpn, record.Condition, record.ProductCategory, record.Color, record.Size, Number(record.Weight),
            record.ItemWeightUnit, Number(record.Length), Number(record.Width), Number(record.Height), record.DimensionsUnit,
            record.ShippingPrice, Boolean(record.AcceptsReturns), record.ReturnDeadlineInDays?.ToString(CultureInfo.InvariantCulture),
            record.ReturnPolicy, Boolean(record.AcceptsExchanges), record.SellerUrl, record.SellerPrivacyPolicy, record.SellerTos,
            Boolean(record.IsDigital), Boolean(record.IsEligibleSearch), Boolean(record.IsEligibleCheckout), record.StoreCountry,
            Join(record.TargetCountries),
        ];
    }

    /// <summary>
    /// Splits records into shards of at most <paramref name="maxItemsPerShard"/> items, ordered by item id, with stable
    /// file names <c>products-00001.jsonl.gz</c>, <c>products-00002.jsonl.gz</c>…: the same catalog always gives the
    /// same files, so each push overwrites the last.
    /// </summary>
    /// <param name="records">The records.</param>
    /// <param name="format">One of <see cref="OpenAIProductFeedFormats"/>.</param>
    /// <param name="maxItemsPerShard">The most items one shard holds.</param>
    public static IReadOnlyList<(string FileName, IReadOnlyList<OpenAIProductFeedRecord> Records)> Shard(
        IEnumerable<OpenAIProductFeedRecord> records,
        string format,
        int maxItemsPerShard = MaxItemsPerShard)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxItemsPerShard, 1);

        var ordered = records.OrderBy(record => record.ItemId, StringComparer.Ordinal).ToList();
        var shards = new List<(string, IReadOnlyList<OpenAIProductFeedRecord>)>();

        var start = 0;

        // An empty catalog is one empty shard, so a push still replaces the files of the last one.
        do
        {
            var count = Math.Min(maxItemsPerShard, ordered.Count - start);

            shards.Add((ShardFileName(shards.Count + 1, format), ordered.GetRange(start, count)));
            start += maxItemsPerShard;
        }
        while (start < ordered.Count);

        return shards;
    }

    /// <summary>
    /// Returns the file name of a shard, such as <c>products-00001.jsonl.gz</c>.
    /// </summary>
    /// <param name="number">The shard's number, from 1.</param>
    /// <param name="format">One of <see cref="OpenAIProductFeedFormats"/>.</param>
    public static string ShardFileName(int number, string format)
        => $"products-{number.ToString("00000", CultureInfo.InvariantCulture)}.{format}";

    private static void WriteText(IEnumerable<OpenAIProductFeedRecord> records, string format, Stream output)
    {
        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 65536, leaveOpen: true);

        if (OpenAIProductFeedFormats.IsCsv(format))
        {
            writer.Write(string.Join(',', Columns));
            writer.Write(CsvWriter.RecordSeparator);

            foreach (var record in records)
            {
                writer.Write(string.Join(',', ToCsvCells(record).Select(cell => CsvWriter.Escape(cell, guardFormula: false))));
                writer.Write(CsvWriter.RecordSeparator);
            }

            return;
        }

        foreach (var record in records)
        {
            writer.Write(ToJsonLine(record));
            writer.Write('\n');
        }
    }

    private static string Text(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> List(IEnumerable<string> values)
    {
        var list = values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToList();

        return list is { Count: > 0 } ? list : null;
    }

    private static string Join(IEnumerable<string> values)
        => values is null ? null : string.Join(',', values);

    private static string Boolean(bool? value)
        => value switch
        {
            true => "true",
            false => "false",
            _ => null,
        };

    private static string Number(decimal? value)
        => value?.ToString("0.###", CultureInfo.InvariantCulture);
}
