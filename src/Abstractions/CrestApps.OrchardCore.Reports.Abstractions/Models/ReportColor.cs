namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Provides helpers that normalize the colors used by report styles so the HTML renderer and the export
/// formats interpret user-supplied color values consistently.
/// </summary>
public static class ReportColor
{
    /// <summary>
    /// Attempts to normalize a color into the eight-character <c>AARRGGBB</c> hexadecimal form used by the
    /// Open XML Excel export. Only hexadecimal colors (with or without a leading <c>#</c>) are supported.
    /// </summary>
    /// <param name="color">The color value to normalize.</param>
    /// <param name="argb">The normalized <c>AARRGGBB</c> value when the color is a valid hexadecimal color.</param>
    /// <returns><see langword="true"/> when the color was normalized; otherwise <see langword="false"/>.</returns>
    public static bool TryGetArgb(string color, out string argb)
    {
        argb = null;

        var hex = GetHex(color);

        if (hex is null)
        {
            return false;
        }

        argb = hex.Length switch
        {
            3 => "FF" + Expand(hex),
            6 => "FF" + hex.ToUpperInvariant(),
            8 => hex.ToUpperInvariant(),
            _ => null,
        };

        return argb is not null;
    }

    /// <summary>
    /// Normalizes a color into a value that is safe to emit inside an inline CSS declaration. Hexadecimal
    /// colors are returned with a leading <c>#</c>, and simple named colors are passed through.
    /// </summary>
    /// <param name="color">The color value to normalize.</param>
    /// <returns>The CSS color, or <see langword="null"/> when the value cannot be safely represented.</returns>
    public static string ToCssColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var value = color.Trim();
        var hex = GetHex(value);

        if (hex is not null)
        {
            return hex.Length switch
            {
                3 or 6 => "#" + hex.ToUpperInvariant(),
                8 => "#" + hex.Substring(2).ToUpperInvariant(),
                _ => null,
            };
        }

        return IsNamedColor(value) ? value.ToLowerInvariant() : null;
    }

    private static string GetHex(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var value = color.Trim();

        if (value.StartsWith('#'))
        {
            value = value.Substring(1);
        }

        if (value.Length is not (3 or 6 or 8) || !IsHex(value))
        {
            return null;
        }

        return value;
    }

    private static bool IsHex(string value)
    {
        foreach (var character in value)
        {
            var isHex = character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNamedColor(string value)
    {
        foreach (var character in value)
        {
            var isLetter = character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

            if (!isLetter)
            {
                return false;
            }
        }

        return value.Length is > 0 and <= 30;
    }

    private static string Expand(string hex)
    {
        return string.Concat(
            hex[0], hex[0],
            hex[1], hex[1],
            hex[2], hex[2]).ToUpperInvariant();
    }
}
