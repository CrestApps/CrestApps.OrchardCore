using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Works out the color of a swatch option, and reads and writes the "Name = color" lines of a field's swatch
/// colors.
/// </summary>
/// <remarks>
/// A color reaches a style attribute, so only hex colors (<c>#191970</c>) and the CSS color names (<c>navy</c>,
/// <c>light blue</c>) are accepted.
/// </remarks>
public static class ProductSwatchColors
{
    private static readonly string[] _termFieldValueProperties = ["Text", "Value", "Color"];

    // The CSS named colors, so a name such as "Honda" or a typo is not taken for a color.
    private static readonly HashSet<string> _namedColors = new(StringComparer.Ordinal)
    {
        "aliceblue", "antiquewhite", "aqua", "aquamarine", "azure", "beige", "bisque", "black", "blanchedalmond",
        "blue", "blueviolet", "brown", "burlywood", "cadetblue", "chartreuse", "chocolate", "coral",
        "cornflowerblue", "cornsilk", "crimson", "cyan", "darkblue", "darkcyan", "darkgoldenrod", "darkgray",
        "darkgreen", "darkgrey", "darkkhaki", "darkmagenta", "darkolivegreen", "darkorange", "darkorchid", "darkred",
        "darksalmon", "darkseagreen", "darkslateblue", "darkslategray", "darkslategrey", "darkturquoise",
        "darkviolet", "deeppink", "deepskyblue", "dimgray", "dimgrey", "dodgerblue", "firebrick", "floralwhite",
        "forestgreen", "fuchsia", "gainsboro", "ghostwhite", "gold", "goldenrod", "gray", "green", "greenyellow",
        "grey", "honeydew", "hotpink", "indianred", "indigo", "ivory", "khaki", "lavender", "lavenderblush",
        "lawngreen", "lemonchiffon", "lightblue", "lightcoral", "lightcyan", "lightgoldenrodyellow", "lightgray",
        "lightgreen", "lightgrey", "lightpink", "lightsalmon", "lightseagreen", "lightskyblue", "lightslategray",
        "lightslategrey", "lightsteelblue", "lightyellow", "lime", "limegreen", "linen", "magenta", "maroon",
        "mediumaquamarine", "mediumblue", "mediumorchid", "mediumpurple", "mediumseagreen", "mediumslateblue",
        "mediumspringgreen", "mediumturquoise", "mediumvioletred", "midnightblue", "mintcream", "mistyrose",
        "moccasin", "navajowhite", "navy", "oldlace", "olive", "olivedrab", "orange", "orangered", "orchid",
        "palegoldenrod", "palegreen", "paleturquoise", "palevioletred", "papayawhip", "peachpuff", "peru", "pink",
        "plum", "powderblue", "purple", "rebeccapurple", "red", "rosybrown", "royalblue", "saddlebrown", "salmon",
        "sandybrown", "seagreen", "seashell", "sienna", "silver", "skyblue", "slateblue", "slategray", "slategrey",
        "snow", "springgreen", "steelblue", "tan", "teal", "thistle", "tomato", "turquoise", "violet", "wheat",
        "white", "whitesmoke", "yellow", "yellowgreen"
    };

    /// <summary>
    /// Gets the color of an option: the field's color for its name or value, then its term's color, then its own
    /// name when that is a color. Returns <see langword="null"/> when none is a color.
    /// </summary>
    /// <param name="value">The option's value, such as a term slug.</param>
    /// <param name="label">The option's name.</param>
    /// <param name="colors">The field's swatch colors, keyed by value name.</param>
    /// <param name="termColor">The color stored on the option's term, if any.</param>
    public static string Resolve(string value, string label, IReadOnlyDictionary<string, string> colors, string termColor)
    {
        if (colors is { Count: > 0 })
        {
            foreach (var key in new[] { label, value })
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var match = colors.FirstOrDefault(pair => string.Equals(pair.Key?.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase));

                if (match.Key is not null && TryNormalize(match.Value, out var mapped))
                {
                    return mapped;
                }
            }
        }

        if (TryNormalize(termColor, out var fromTerm))
        {
            return fromTerm;
        }

        return TryNormalize(label, out var fromName) ? fromName : null;
    }

    /// <summary>
    /// Turns a color into the form used in a style attribute: a hex color with 3, 4, 6 or 8 digits, or a CSS color
    /// name, lowercased with its spaces and dashes removed (<c>Light Blue</c> becomes <c>lightblue</c>).
    /// </summary>
    /// <param name="color">The color as entered.</param>
    /// <param name="css">The color to use in CSS.</param>
    public static bool TryNormalize(string color, out string css)
    {
        css = null;

        if (string.IsNullOrWhiteSpace(color))
        {
            return false;
        }

        var trimmed = color.Trim();

        if (trimmed[0] == '#')
        {
            var digits = trimmed[1..];

            if (digits.Length is 3 or 4 or 6 or 8 && digits.All(char.IsAsciiHexDigit))
            {
                css = "#" + digits.ToLowerInvariant();

                return true;
            }

            return false;
        }

        var name = new StringBuilder();

        foreach (var character in trimmed)
        {
            if (char.IsAsciiLetter(character))
            {
                name.Append(char.ToLowerInvariant(character));
            }
            else if (character is not (' ' or '-'))
            {
                return false;
            }
        }

        if (!_namedColors.Contains(name.ToString()))
        {
            return false;
        }

        css = name.ToString();

        return true;
    }

    /// <summary>
    /// Reads "Name = color" lines. Blank lines are skipped; a line without a name or with a color that is not
    /// accepted is returned in <paramref name="invalid"/>.
    /// </summary>
    /// <param name="text">The lines as entered.</param>
    /// <param name="invalid">The lines that could not be read.</param>
    public static Dictionary<string, string> Parse(string text, out IReadOnlyList<string> invalid)
    {
        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var line in (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('=');
            var name = separator > 0 ? line[..separator].Trim() : null;
            var color = separator > 0 ? line[(separator + 1)..].Trim() : null;

            if (string.IsNullOrEmpty(name) || !TryNormalize(color, out _))
            {
                errors.Add(line);

                continue;
            }

            colors[name] = color;
        }

        invalid = errors;

        return colors;
    }

    /// <summary>
    /// Writes swatch colors as "Name = color" lines.
    /// </summary>
    /// <param name="colors">The colors, keyed by value name.</param>
    public static string Format(IReadOnlyDictionary<string, string> colors)
        => colors is null ? string.Empty : string.Join(Environment.NewLine, colors.Select(pair => $"{pair.Key} = {pair.Value}"));

    /// <summary>
    /// Reads the value of a text field on a term, such as a <c>Color</c> field: the first part of the term that has
    /// a field of that name gives its text.
    /// </summary>
    /// <param name="term">The term's stored content item.</param>
    /// <param name="fieldName">The name of the field.</param>
    public static string ReadTermField(JsonObject term, string fieldName)
    {
        if (term is null || string.IsNullOrWhiteSpace(fieldName))
        {
            return null;
        }

        foreach (var (_, part) in term)
        {
            if (part is not JsonObject partObject || partObject[fieldName.Trim()] is not JsonObject field)
            {
                continue;
            }

            foreach (var property in _termFieldValueProperties)
            {
                if (field[property] is JsonValue value && value.GetValueKind() == JsonValueKind.String)
                {
                    return value.GetValue<string>();
                }
            }
        }

        return null;
    }
}
