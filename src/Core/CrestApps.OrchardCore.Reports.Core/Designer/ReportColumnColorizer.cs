using System.Globalization;
using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Works out the style of each cell of a table column from the column's <see cref="ReportColumnColors"/>: the color of
/// every cell, then the color scale over the numbers the table shows, then the first rule the value meets. A cell given
/// a background color gets a text color that stays readable on it.
/// </summary>
internal sealed class ReportColumnColorizer
{
    private const string LightText = "#FFFFFF";
    private const string DarkText = "#212529";

    private readonly ReportColumnColors _colors;
    private readonly List<(Func<object, bool> Matches, ReportColorRule Rule)> _rules;
    private readonly decimal _min;
    private readonly decimal _max;
    private readonly bool _scaled;

    private ReportColumnColorizer(ReportColumnColors colors, List<(Func<object, bool>, ReportColorRule)> rules, decimal min, decimal max, bool scaled)
    {
        _colors = colors;
        _rules = rules;
        _min = min;
        _max = max;
        _scaled = scaled;
    }

    /// <summary>
    /// Creates the colorizer of a column, or returns <see langword="null"/> when the column colors no cell.
    /// </summary>
    /// <param name="column">The column.</param>
    /// <param name="values">The values of the cells the table shows in the column, which the color scale spans.</param>
    /// <param name="today">The time the report ran in the site's time zone, which relative date rules are measured from.</param>
    /// <returns>The colorizer, or <see langword="null"/>.</returns>
    public static ReportColumnColorizer Create(ReportResultColumn column, IEnumerable<object> values, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(column);

        var colors = column.Colors;

        if (colors is null || (colors.Color is null && colors.BackgroundColor is null && colors.Scale is null && (colors.Rules?.Count ?? 0) == 0))
        {
            return null;
        }

        var rules = new List<(Func<object, bool>, ReportColorRule)>();

        foreach (var rule in colors.Rules ?? [])
        {
            if (string.IsNullOrEmpty(rule?.Color))
            {
                continue;
            }

            var matches = DataFilterPredicates.Build(rule.Operator, column.DataType, rule.Values ?? [], today);

            if (matches is not null)
            {
                rules.Add((matches, rule));
            }
        }

        var min = decimal.MaxValue;
        var max = decimal.MinValue;

        if (colors.Scale is not null)
        {
            foreach (var value in values ?? [])
            {
                if (ToNumber(value) is decimal number)
                {
                    min = Math.Min(min, number);
                    max = Math.Max(max, number);
                }
            }
        }

        return new ReportColumnColorizer(colors, rules, min, max, colors.Scale is not null && min <= max);
    }

    /// <summary>
    /// Gets the style of the header of a column, or <see langword="null"/> when the column doesn't color its header.
    /// </summary>
    /// <param name="column">The column.</param>
    /// <returns>The style, or <see langword="null"/>.</returns>
    public static ReportStyle HeaderStyleOf(ReportResultColumn column)
    {
        var colors = column?.Colors;

        if (colors is null || (colors.HeaderColor is null && colors.HeaderBackgroundColor is null))
        {
            return null;
        }

        return ReportStyle.Create(colors.HeaderColor ?? ReadableText(colors.HeaderBackgroundColor), colors.HeaderBackgroundColor);
    }

    /// <summary>
    /// Gets the style of the cell that shows a value, or <see langword="null"/> when nothing colors it.
    /// </summary>
    /// <param name="value">The value of the cell.</param>
    /// <returns>The style, or <see langword="null"/>.</returns>
    public ReportStyle StyleOf(object value)
    {
        var text = _colors.Color;
        var background = _colors.BackgroundColor;

        if (_scaled && ToNumber(value) is decimal number)
        {
            Paint(_colors.Scale.Target, ScaleColor(number), ref text, ref background);
        }

        foreach (var (matches, rule) in _rules)
        {
            if (matches(value))
            {
                Paint(rule.Target, rule.Color, ref text, ref background);

                break;
            }
        }

        if (text is null && background is null)
        {
            return null;
        }

        // Text keeps its own color only when something chose it; otherwise it follows the background.
        text ??= ReadableText(background);

        return ReportStyle.Create(text, background);
    }

    private static void Paint(ReportColorTarget target, string color, ref string text, ref string background)
    {
        if (color is null)
        {
            return;
        }

        if (target == ReportColorTarget.Text)
        {
            text = color;
        }
        else
        {
            background = color;
        }
    }

    // The color of a value on the scale: the low color at the smallest value, the high color at the largest, and the
    // middle color, when there is one, halfway.
    private string ScaleColor(decimal value)
    {
        var scale = _colors.Scale;
        var position = _max == _min ? 1d : (double)((value - _min) / (_max - _min));

        if (scale.MidColor is null)
        {
            return Blend(scale.LowColor, scale.HighColor, position);
        }

        return position < 0.5d
            ? Blend(scale.LowColor, scale.MidColor, position * 2d)
            : Blend(scale.MidColor, scale.HighColor, (position - 0.5d) * 2d);
    }

    private static string Blend(string from, string to, double position)
    {
        if (!TryGetRgb(from, out var start) || !TryGetRgb(to, out var end))
        {
            return position < 0.5d ? from : to;
        }

        position = Math.Clamp(position, 0d, 1d);

        int Mix(int left, int right) => (int)Math.Round(left + ((right - left) * position));

        return string.Create(CultureInfo.InvariantCulture, $"#{Mix(start.R, end.R):X2}{Mix(start.G, end.G):X2}{Mix(start.B, end.B):X2}");
    }

    // A text color readable on a background: white on a dark color, near black on a light one. Unknown when the
    // background is a color name.
    private static string ReadableText(string background)
    {
        if (!TryGetRgb(background, out var rgb))
        {
            return null;
        }

        var luminance = ((0.299d * rgb.R) + (0.587d * rgb.G) + (0.114d * rgb.B)) / 255d;

        return luminance < 0.6d ? LightText : DarkText;
    }

    private static bool TryGetRgb(string color, out (int R, int G, int B) rgb)
    {
        rgb = default;

        if (!ReportColor.TryGetArgb(color, out var argb))
        {
            return false;
        }

        rgb = (
            int.Parse(argb.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(argb.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(argb.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        return true;
    }

    private static decimal? ToNumber(object value)
    {
        return value is null or string ? null : DataValues.Coerce(value, DataFieldType.Decimal) as decimal?;
    }
}
