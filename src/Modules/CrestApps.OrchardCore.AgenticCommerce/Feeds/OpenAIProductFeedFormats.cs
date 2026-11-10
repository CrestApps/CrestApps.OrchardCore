namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// The file formats of the OpenAI product feed.
/// </summary>
public static class OpenAIProductFeedFormats
{
    /// <summary>
    /// JSON lines: one product per line.
    /// </summary>
    public const string JsonLines = "jsonl";

    /// <summary>
    /// JSON lines, gzipped.
    /// </summary>
    public const string JsonLinesGzip = "jsonl.gz";

    /// <summary>
    /// CSV with a header row.
    /// </summary>
    public const string Csv = "csv";

    /// <summary>
    /// CSV, gzipped.
    /// </summary>
    public const string CsvGzip = "csv.gz";

    /// <summary>
    /// Returns whether a format is one of the feed's.
    /// </summary>
    /// <param name="format">The format.</param>
    public static bool IsValid(string format)
        => format is JsonLines or JsonLinesGzip or Csv or CsvGzip;

    /// <summary>
    /// Returns whether a format is gzipped.
    /// </summary>
    /// <param name="format">The format.</param>
    public static bool IsGzip(string format)
        => format is JsonLinesGzip or CsvGzip;

    /// <summary>
    /// Returns whether a format is CSV.
    /// </summary>
    /// <param name="format">The format.</param>
    public static bool IsCsv(string format)
        => format is Csv or CsvGzip;
}
