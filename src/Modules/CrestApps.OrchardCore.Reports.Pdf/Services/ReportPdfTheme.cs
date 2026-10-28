namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// The sizes and colors of a report PDF: the colors of the report on screen, Bootstrap's body text, muted text,
/// borders and table states, and the palette of the report charts.
/// </summary>
internal static class ReportPdfTheme
{
    /// <summary>
    /// The size of the text of the report, in points.
    /// </summary>
    public const double BaseFontSize = 9;

    /// <summary>
    /// The family the report is written in, or the installed font that stands in for it.
    /// </summary>
    public const string FontFamily = "Arial";

    public static readonly ReportPdfColor Text = new(0x21, 0x25, 0x29);

    public static readonly ReportPdfColor Muted = new(0x6C, 0x75, 0x7D);

    public static readonly ReportPdfColor Border = new(0xDE, 0xE2, 0xE6);

    public static readonly ReportPdfColor HeaderFill = new(0xF1, 0xF3, 0xF5);

    public static readonly ReportPdfColor CardFill = new(0xF8, 0xF9, 0xFA);

    public static readonly ReportPdfColor SubtotalFill = new(0xCF, 0xF4, 0xFC);

    public static readonly ReportPdfColor GrandTotalFill = new(0xCF, 0xE2, 0xFF);

    public static readonly ReportPdfColor BarTrack = new(0xE9, 0xEC, 0xEF);

    public static readonly ReportPdfColor White = new(0xFF, 0xFF, 0xFF);

    public static readonly ReportPdfColor Primary = new(0x0D, 0x6E, 0xFD);

    public static readonly ReportPdfColor[] Palette =
    [
        new(0x25, 0x63, 0xEB),
        new(0x14, 0xB8, 0xA6),
        new(0xF5, 0x9E, 0x0B),
        new(0xEF, 0x44, 0x44),
        new(0x8B, 0x5C, 0xF6),
        new(0x06, 0xB6, 0xD4),
        new(0x84, 0xCC, 0x16),
        new(0xF9, 0x73, 0x16),
        new(0xEC, 0x48, 0x99),
        new(0x64, 0x74, 0x8B),
    ];

    /// <summary>
    /// Gets the color of a series or a slice of a chart.
    /// </summary>
    /// <param name="index">The index of the series or slice.</param>
    /// <returns>The color.</returns>
    public static ReportPdfColor GetPaletteColor(int index)
    {
        return Palette[index % Palette.Length];
    }
}
