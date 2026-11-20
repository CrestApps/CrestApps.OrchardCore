using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.BusinessDocuments.Models;

namespace CrestApps.OrchardCore.BusinessDocuments.Core;

/// <summary>
/// The rules every business document renderer shares: which accent colors and logo addresses are accepted, the colors
/// of the status tones, and the names a document's kind gives its shape alternate and CSS class.
/// </summary>
public static class BusinessDocumentStyle
{
    /// <summary>
    /// The color of the text, written <c>#RRGGBB</c>.
    /// </summary>
    public const string TextColor = "#111827";

    /// <summary>
    /// The color of muted text, such as labels, written <c>#RRGGBB</c>.
    /// </summary>
    public const string MutedColor = "#6B7280";

    /// <summary>
    /// The color of rules between rows, written <c>#RRGGBB</c>.
    /// </summary>
    public const string BorderColor = "#E5E7EB";

    /// <summary>
    /// The fill of the header row of the line table when the document has no accent color, written <c>#RRGGBB</c>.
    /// </summary>
    public const string HeaderFill = "#F3F4F6";

    /// <summary>
    /// Returns whether a value is an accent color: <c>#</c> and six hexadecimal digits.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the value is an accent color.</returns>
    public static bool IsAccentColor(string value)
        => TryNormalizeAccentColor(value, out _);

    /// <summary>
    /// Reads an accent color written <c>#RRGGBB</c>, with surrounding spaces allowed, and writes it in upper case.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="color">The color, such as <c>#2563EB</c>, when the value is one; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is an accent color.</returns>
    public static bool TryNormalizeAccentColor(string value, out string color)
    {
        color = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        if (text.Length != 7 || text[0] != '#')
        {
            return false;
        }

        for (var index = 1; index < text.Length; index++)
        {
            if (!char.IsAsciiHexDigit(text[index]))
            {
                return false;
            }
        }

        color = text.ToUpperInvariant();

        return true;
    }

    /// <summary>
    /// Returns whether text on a background of an accent color is easier to read dark than white.
    /// </summary>
    /// <param name="color">The accent color, written <c>#RRGGBB</c>.</param>
    /// <returns><see langword="true"/> when the background is light.</returns>
    public static bool PrefersDarkText(string color)
    {
        if (!TryNormalizeAccentColor(color, out var normalized))
        {
            return true;
        }

        var red = int.Parse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var green = int.Parse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var blue = int.Parse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        // The perceived brightness of the color, from 0 to 255.
        return ((red * 299) + (green * 587) + (blue * 114)) / 1000 > 160;
    }

    /// <summary>
    /// Returns whether an address may be used as the source of an image: an <c>http</c> or <c>https</c> address, or a
    /// relative one. Other schemes, such as <c>javascript:</c> or <c>data:</c>, are refused.
    /// </summary>
    /// <param name="url">The address.</param>
    /// <returns><see langword="true"/> when the address may be shown.</returns>
    public static bool IsSafeImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.RelativeOrAbsolute, out var uri))
        {
            return false;
        }

        return !uri.IsAbsoluteUri || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;
    }

    /// <summary>
    /// Gets the background of a status badge, written <c>#RRGGBB</c>.
    /// </summary>
    /// <param name="tone">The tone of the status.</param>
    /// <returns>The color.</returns>
    public static string GetToneBackground(BusinessDocumentTone tone)
        => tone switch
        {
            BusinessDocumentTone.Success => "#DCFCE7",
            BusinessDocumentTone.Warning => "#FEF3C7",
            BusinessDocumentTone.Danger => "#FEE2E2",
            BusinessDocumentTone.Info => "#DBEAFE",
            _ => "#F3F4F6",
        };

    /// <summary>
    /// Gets the text color of a status badge, written <c>#RRGGBB</c>.
    /// </summary>
    /// <param name="tone">The tone of the status.</param>
    /// <returns>The color.</returns>
    public static string GetToneText(BusinessDocumentTone tone)
        => tone switch
        {
            BusinessDocumentTone.Success => "#166534",
            BusinessDocumentTone.Warning => "#92400E",
            BusinessDocumentTone.Danger => "#991B1B",
            BusinessDocumentTone.Info => "#1E40AF",
            _ => "#374151",
        };

    /// <summary>
    /// Gets the Bootstrap color of a status badge, for the <c>text-bg-{color}</c> classes.
    /// </summary>
    /// <param name="tone">The tone of the status.</param>
    /// <returns>The Bootstrap color name.</returns>
    public static string GetBootstrapColor(BusinessDocumentTone tone)
        => tone switch
        {
            BusinessDocumentTone.Success => "success",
            BusinessDocumentTone.Warning => "warning",
            BusinessDocumentTone.Danger => "danger",
            BusinessDocumentTone.Info => "info",
            _ => "secondary",
        };

    /// <summary>
    /// Gets the part a document's kind gives a shape alternate, such as <c>Invoice</c> in
    /// <c>BusinessDocument__Invoice</c>: the letters, digits and underscores of the kind.
    /// </summary>
    /// <param name="kind">The kind of document.</param>
    /// <returns>The part, or <see langword="null"/> when the kind has none.</returns>
    public static string GetAlternateName(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return null;
        }

        var builder = new StringBuilder(kind.Length);

        foreach (var character in kind)
        {
            if (char.IsAsciiLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>
    /// Gets the paper size a document is printed on: its own, or <see cref="BusinessDocumentPaperSize.Letter"/>.
    /// </summary>
    /// <param name="paperSize">The paper size of the document.</param>
    /// <returns>The paper size.</returns>
    public static BusinessDocumentPaperSize GetPaperSize(BusinessDocumentPaperSize paperSize)
        => paperSize == BusinessDocumentPaperSize.Default ? BusinessDocumentPaperSize.Letter : paperSize;
}
