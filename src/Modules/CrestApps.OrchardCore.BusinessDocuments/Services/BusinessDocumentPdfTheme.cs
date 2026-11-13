using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.Pdf;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// The sizes and colors of a business document's PDF.
/// </summary>
internal static class BusinessDocumentPdfTheme
{
    public const double BaseSize = 9.5;
    public const double SmallSize = 8;
    public const double LabelSize = 7.5;
    public const double NameSize = 13;
    public const double TitleSize = 20;
    public const double NumberSize = 11;
    public const double SectionTitleSize = 10;
    public const double TotalSize = 9.5;
    public const double EmphasizedTotalSize = 11.5;
    public const double WatermarkMaximumSize = 110;

    // US Letter and ISO A4, in points.
    public const double LetterWidth = 612;
    public const double LetterHeight = 792;
    public const double A4Width = 595.28;
    public const double A4Height = 841.89;

    public static readonly PdfColor Text = Parse(BusinessDocumentStyle.TextColor);
    public static readonly PdfColor Muted = Parse(BusinessDocumentStyle.MutedColor);
    public static readonly PdfColor Border = Parse(BusinessDocumentStyle.BorderColor);
    public static readonly PdfColor HeaderFill = Parse(BusinessDocumentStyle.HeaderFill);
    public static readonly PdfColor SectionFill = PdfColor.FromRgb(0xF9FAFB);
    public static readonly PdfColor Rule = PdfColor.FromRgb(0x9CA3AF);
    public static readonly PdfColor Watermark = PdfColor.FromRgb(0xE5E7EB);
    public static readonly PdfColor White = PdfColor.FromRgb(0xFFFFFF);

    /// <summary>
    /// Reads a color written <c>#RRGGBB</c>.
    /// </summary>
    /// <param name="value">The color.</param>
    /// <returns>The color, or the text color when the value is not one.</returns>
    public static PdfColor Parse(string value)
        => PdfColor.TryParseHex(value, out var color) ? color : PdfColor.FromRgb(0x111827);
}
