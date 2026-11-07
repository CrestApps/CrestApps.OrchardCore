using System.Globalization;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Tabular;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Reads and writes stock counts as CSV, with <see cref="CsvReader"/> and <see cref="CsvWriter"/>: one line per product or
/// variant, found by its ids or its SKU.
/// </summary>
public static class StockCsv
{
    public const string Sku = "Sku";
    public const string Title = "Title";
    public const string ContentItemId = "ContentItemId";
    public const string VariantId = "VariantId";
    public const string OnHand = "OnHand";
    public const string Reserved = "Reserved";
    public const string Available = "Available";
    public const string LowStockThreshold = "LowStockThreshold";

    private static readonly string[] _header = [Sku, Title, ContentItemId, VariantId, OnHand, Reserved, Available, LowStockThreshold];

    /// <summary>
    /// Writes the header line.
    /// </summary>
    public static void WriteHeader(TextWriter writer)
        => CsvWriter.WriteRow(writer, _header);

    /// <summary>
    /// Writes one stock item. Only <see cref="OnHand"/> is read back on import; the other counts are for reading.
    /// </summary>
    public static void Write(TextWriter writer, StockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        CsvWriter.WriteRow(writer,
        [
            item.Sku,
            item.Title,
            item.ContentItemId,
            item.VariantId,
            item.OnHand.ToString(CultureInfo.InvariantCulture),
            item.Reserved.ToString(CultureInfo.InvariantCulture),
            item.Available.ToString(CultureInfo.InvariantCulture),
            item.LowStockThreshold.ToString(CultureInfo.InvariantCulture),
        ]);
    }

    /// <summary>
    /// Reads the counts. The columns are found by name, in any order and any case; <see cref="OnHand"/> is required,
    /// with <see cref="Sku"/> or <see cref="ContentItemId"/> to find the item. Returns <see langword="null"/> when the
    /// header lacks them.
    /// </summary>
    public static IReadOnlyList<StockCountRow> Read(TextReader reader, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(reader);

        using var records = CsvReader.ReadRecords(reader).GetEnumerator();

        if (!records.MoveNext())
        {
            return null;
        }

        var header = records.Current;

        int Column(string name)
            => Array.FindIndex(header, value => string.Equals(value?.Trim(), name, StringComparison.OrdinalIgnoreCase));

        var onHand = Column(OnHand);
        var sku = Column(Sku);
        var contentItemId = Column(ContentItemId);
        var variantId = Column(VariantId);

        if (onHand < 0 || (sku < 0 && contentItemId < 0))
        {
            return null;
        }

        var rows = new List<StockCountRow>();
        var line = 1;

        while (records.MoveNext())
        {
            var values = records.Current;

            line++;

            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (rows.Count >= maxRows)
            {
                rows.Add(new StockCountRow { Line = line, TooMany = true });

                break;
            }

            string Value(int index)
                => index >= 0 && index < values.Length && !string.IsNullOrWhiteSpace(values[index]) ? values[index].Trim() : null;

            var count = Value(onHand);

            rows.Add(new StockCountRow
            {
                Line = line,
                Sku = Value(sku),
                ContentItemId = Value(contentItemId),
                VariantId = Value(variantId),
                OnHand = int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            });
        }

        return rows;
    }
}

/// <summary>
/// A count to apply to a stock item: the units on hand, and how to find the item.
/// </summary>
public sealed class StockCountRow
{
    /// <summary>
    /// Gets the line of the file the count is on, or its position in a recipe.
    /// </summary>
    public int Line { get; init; }

    public string Sku { get; init; }

    public string ContentItemId { get; init; }

    public string VariantId { get; init; }

    /// <summary>
    /// Gets the units on hand, or <see langword="null"/> when the value isn't a whole number.
    /// </summary>
    public int? OnHand { get; init; }

    /// <summary>
    /// Gets whether the file had more rows than are read at once; this row and the rest weren't read.
    /// </summary>
    public bool TooMany { get; init; }
}
