using System.Text;

namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents the optional visual styling applied to a report table header or cell. Styling lets a report
/// color-code individual cells, headers, subtotal rows, and grand-total rows. It is honored by the HTML
/// renderer and, where the export format supports it, by exporters such as the Open XML Excel workbook
/// exporter. Formats that cannot represent styling (for example CSV) ignore it.
/// </summary>
public sealed class ReportStyle
{
    /// <summary>
    /// Gets or sets the font (text) color. Use a hexadecimal color (for example <c>#2563EB</c>) so the
    /// value can also be applied by the Excel exporter; simple named colors are honored by the HTML
    /// renderer only.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the background (fill) color. Use a hexadecimal color (for example <c>#EFF6FF</c>) so
    /// the value can also be applied by the Excel exporter; simple named colors are honored by the HTML
    /// renderer only.
    /// </summary>
    public string BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text is rendered in a bold (heavier) font weight.
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Gets a value indicating whether the style has no effect (no color, no background, and not bold).
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            return string.IsNullOrWhiteSpace(Color) && string.IsNullOrWhiteSpace(BackgroundColor) && !Bold;
        }
    }

    /// <summary>
    /// Creates a report style from the supplied color, background color, and weight.
    /// </summary>
    /// <param name="color">The font color, preferably a hexadecimal color.</param>
    /// <param name="backgroundColor">The background color, preferably a hexadecimal color.</param>
    /// <param name="bold">Whether the text is bold.</param>
    /// <returns>The created report style.</returns>
    public static ReportStyle Create(string color = null, string backgroundColor = null, bool bold = false)
    {
        return new ReportStyle
        {
            Color = color,
            BackgroundColor = backgroundColor,
            Bold = bold,
        };
    }

    /// <summary>
    /// Builds an inline CSS declaration for the style, using only safely normalized color values. Returns
    /// an empty string when the style has no visual effect.
    /// </summary>
    /// <returns>The inline CSS declaration.</returns>
    public string ToInlineCss()
    {
        var builder = new StringBuilder();
        var color = ReportColor.ToCssColor(Color);

        if (color is not null)
        {
            builder.Append("color:").Append(color).Append(';');
        }

        var background = ReportColor.ToCssColor(BackgroundColor);

        if (background is not null)
        {
            builder.Append("background-color:").Append(background).Append(';');
        }

        if (Bold)
        {
            builder.Append("font-weight:600;");
        }

        return builder.ToString();
    }
}
