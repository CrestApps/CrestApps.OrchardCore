using System.Globalization;
using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Draws a chart section as a chart, like the report charts on screen: lines, areas, columns and sideways bars, side
/// by side or stacked, on a value axis with gridlines, and pies and doughnuts of the first series with the share of
/// each slice. Several series, and the slices of a pie, are named in a legend.
/// </summary>
internal sealed class ReportPdfChartWriter
{
    private const double FontSize = ReportPdfTheme.BaseFontSize - 2;
    private const double PieLabelSize = ReportPdfTheme.BaseFontSize - 2.5;
    private const double SpaceBefore = 4;
    private const double Swatch = 6;
    private const double MaxCategoryLabelWidth = 90;
    private const double MinHeight = 180;

    // Like the charts on screen, a group of bars takes this part of its category, and a bar this part of its slot.
    private const double GroupShare = 0.8;
    private const double BarShare = 0.9;

    private readonly ReportPdfFlow _flow;
    private readonly ReportChart _chart;
    private readonly ChartKind _kind;
    private readonly bool _stacked;
    private readonly int _categoryCount;
    private readonly List<string> _labels = [];
    private readonly List<Series> _series = [];
    private readonly bool _integers;
    private readonly double _chartHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportPdfChartWriter"/> class.
    /// </summary>
    /// <param name="flow">The flow the chart is drawn in.</param>
    /// <param name="chart">The chart of the section.</param>
    /// <param name="maxHeight">The height a page has under the head of the section, in points, which the chart is kept to.</param>
    public ReportPdfChartWriter(
        ReportPdfFlow flow,
        ReportChart chart,
        double maxHeight)
    {
        _flow = flow;
        _chart = chart;

        var datasets = (chart?.Datasets ?? []).Where(dataset => dataset?.Values is { Count: > 0 }).ToList();

        if (datasets.Count == 0)
        {
            return;
        }

        var labels = chart.Labels ?? [];

        _kind = chart.Type switch
        {
            ReportChartType.Line => ChartKind.Line,
            ReportChartType.Area => ChartKind.Area,
            ReportChartType.Pie or ReportChartType.Doughnut => ChartKind.Pie,
            ReportChartType.HorizontalBar => ChartKind.Bar,
            _ => ChartKind.Column,
        };

        _stacked = chart.Stacked && _kind is ChartKind.Column or ChartKind.Bar;
        _categoryCount = Math.Max(labels.Count, datasets.Max(dataset => dataset.Values.Count));

        // A pie shows one series, as the doughnuts and pies on screen do.
        if (_kind == ChartKind.Pie)
        {
            datasets = [datasets[0]];
        }

        for (var index = 0; index < _categoryCount; index++)
        {
            _labels.Add(index < labels.Count ? SingleLine(labels[index]) : string.Empty);
        }

        for (var index = 0; index < datasets.Count; index++)
        {
            var values = Enumerable.Range(0, _categoryCount)
                .Select(category => category < datasets[index].Values.Count ? datasets[index].Values[category] : double.NaN)
                .ToArray();

            _series.Add(new Series(SingleLine(datasets[index].Label), values, ReportPdfTheme.GetPaletteColor(index)));
        }

        _integers = _series.SelectMany(series => series.Values).Where(double.IsFinite).All(value => Math.Abs(value - Math.Round(value)) < 1e-9);

        var height = _kind switch
        {
            ChartKind.Pie => _series[0].Values.Any(value => double.IsFinite(value) && value > 0) ? 260 : 0,
            ChartKind.Bar => Math.Clamp((_categoryCount * ((_series.Count * 12) + 8)) + 60, MinHeight, flow.PageContentHeight * 0.9),
            _ => 240,
        };

        // The chart fits on a page with the head of its section, unless that would leave it too short to read. A point is
        // spared for rounding, as the head is measured and laid out in different steps.
        _chartHeight = Math.Min(height, Math.Max(maxHeight - SpaceBefore - 1, MinHeight));
    }

    private enum ChartKind
    {
        Column,
        Bar,
        Line,
        Area,
        Pie,
    }

    /// <summary>
    /// Gets the height of the chart, in points, or 0 when it has nothing to draw.
    /// </summary>
    public double Height => _chartHeight > 0 ? SpaceBefore + _chartHeight : 0;

    private ReportPdfFont Font => _flow.Regular;

    private double LineHeight => Font.LineHeight * FontSize;

    /// <summary>
    /// Draws the chart, on the next page when it does not fit on this one.
    /// </summary>
    public void Write()
    {
        _flow.EnsureSpace(Height);
        _flow.Advance(SpaceBefore);

        var box = new Box(_flow.Left, _flow.Top, _flow.Width, _chartHeight);

        switch (_kind)
        {
            case ChartKind.Pie:
                DrawPie(box);
                break;
            case ChartKind.Bar:
                DrawHorizontal(box);
                break;
            default:
                DrawVertical(box);
                break;
        }

        _flow.Advance(_chartHeight);
    }

    private void DrawVertical(Box box)
    {
        var page = _flow.Page;
        var legendHeight = DrawSeriesLegend(box);
        var scale = GetScale();
        var tickLabels = scale.Ticks.Select(FormatTick).ToList();
        var plotLeft = box.Left + tickLabels.Max(label => Font.Measure(label, FontSize)) + 6;
        var plotRight = box.Right - 6;
        var plotTop = box.Top + 6;
        var plotBottom = box.Bottom - legendHeight - LineHeight - 4;
        var bandWidth = (plotRight - plotLeft) / _categoryCount;

        double Y(double value) => plotBottom - ((value - scale.Min) / (scale.Max - scale.Min) * (plotBottom - plotTop));

        for (var index = 0; index < scale.Ticks.Count; index++)
        {
            var y = Y(scale.Ticks[index]);
            var width = Font.Measure(tickLabels[index], FontSize);

            page.DrawLine(plotLeft, y, plotRight, y, ReportPdfTheme.Border, 0.5);
            page.DrawText(tickLabels[index], Font, FontSize, plotLeft - 4 - width, CenteredTop(y), ReportPdfTheme.Muted);
        }

        // Labels that would run into each other are skipped evenly, as the charts on screen skip them.
        var step = GetLabelStep(bandWidth);

        for (var index = 0; index < _categoryCount; index += step)
        {
            var label = ReportPdfText.Fit(_labels[index], Font, FontSize, Math.Min(MaxCategoryLabelWidth, (bandWidth * step) - 2));
            var center = plotLeft + ((index + 0.5) * bandWidth);

            page.DrawText(label, Font, FontSize, center - (Font.Measure(label, FontSize) / 2), plotBottom + 3, ReportPdfTheme.Muted);
        }

        if (_kind == ChartKind.Column)
        {
            DrawColumns(plotLeft, bandWidth, Y);
        }
        else
        {
            DrawLines(plotLeft, bandWidth, Y);
        }

        page.DrawLine(plotLeft, Y(0), plotRight, Y(0), ReportPdfTheme.Muted, 0.75);
    }

    private void DrawColumns(double plotLeft, double bandWidth, Func<double, double> y)
    {
        var groupWidth = bandWidth * GroupShare;
        var slotWidth = _stacked ? groupWidth : groupWidth / _series.Count;

        foreach (var (category, index, from, to) in GetBars())
        {
            var slotLeft = plotLeft + (category * bandWidth) + ((bandWidth - groupWidth) / 2) + (_stacked ? 0 : index * slotWidth);
            var top = y(Math.Max(from, to));

            _flow.Page.FillRectangle(slotLeft + (slotWidth * (1 - BarShare) / 2), top, slotWidth * BarShare, y(Math.Min(from, to)) - top, _series[index].Color);
        }
    }

    private void DrawLines(double plotLeft, double bandWidth, Func<double, double> y)
    {
        var page = _flow.Page;
        var runs = _series.Select(series => GetRuns(series.Values, plotLeft, bandWidth, y)).ToList();

        // Areas are filled first, so every line is drawn over them.
        if (_kind == ChartKind.Area)
        {
            for (var index = 0; index < _series.Count; index++)
            {
                foreach (var run in runs[index].Where(run => run.Count > 1))
                {
                    var polygon = new List<(double X, double Top)> { (run[0].X, y(0)) };

                    polygon.AddRange(run);
                    polygon.Add((run[run.Count - 1].X, y(0)));
                    page.FillPolygon(polygon, _series[index].Color.Lighten(0.65));
                }
            }
        }

        for (var index = 0; index < _series.Count; index++)
        {
            foreach (var run in runs[index])
            {
                page.DrawPolyline(run, _series[index].Color, 2);

                if (bandWidth >= 6)
                {
                    foreach (var (x, top) in run)
                    {
                        page.FillCircle(x, top, 2, _series[index].Color);
                    }
                }
            }
        }
    }

    private static List<List<(double X, double Top)>> GetRuns(double[] values, double plotLeft, double bandWidth, Func<double, double> y)
    {
        // A missing value breaks the line, as a gap in the data.
        var runs = new List<List<(double X, double Top)>>();
        List<(double X, double Top)> run = null;

        for (var category = 0; category < values.Length; category++)
        {
            if (!double.IsFinite(values[category]))
            {
                run = null;

                continue;
            }

            if (run is null)
            {
                run = [];
                runs.Add(run);
            }

            run.Add((plotLeft + ((category + 0.5) * bandWidth), y(values[category])));
        }

        return runs;
    }

    private void DrawHorizontal(Box box)
    {
        var page = _flow.Page;
        var legendHeight = DrawSeriesLegend(box);
        var scale = GetScale();
        var tickLabels = scale.Ticks.Select(FormatTick).ToList();
        var labelWidth = Math.Min(_labels.Max(label => Font.Measure(label, FontSize)), box.Width * 0.3);
        var plotLeft = box.Left + labelWidth + 6;
        var plotRight = box.Right - (Font.Measure(tickLabels[tickLabels.Count - 1], FontSize) / 2) - 2;
        var plotTop = box.Top + 4;
        var plotBottom = box.Bottom - legendHeight - LineHeight - 4;
        var bandHeight = (plotBottom - plotTop) / _categoryCount;

        double X(double value) => plotLeft + ((value - scale.Min) / (scale.Max - scale.Min) * (plotRight - plotLeft));

        for (var index = 0; index < scale.Ticks.Count; index++)
        {
            var x = X(scale.Ticks[index]);

            page.DrawLine(x, plotTop, x, plotBottom, ReportPdfTheme.Border, 0.5);
            page.DrawText(tickLabels[index], Font, FontSize, x - (Font.Measure(tickLabels[index], FontSize) / 2), plotBottom + 3, ReportPdfTheme.Muted);
        }

        // The first category is at the top, as on screen. A label wraps, and is cut when its lines do not fit its band.
        var maxLines = Math.Max(1, (int)Math.Floor(bandHeight / LineHeight));

        for (var category = 0; category < _categoryCount; category++)
        {
            var lines = ReportPdfText.Wrap(_labels[category], Font, FontSize, labelWidth);

            if (lines.Count > maxLines)
            {
                var rest = string.Join(' ', lines.Skip(maxLines - 1));

                lines = [.. lines.Take(maxLines - 1), ReportPdfText.Fit(rest, Font, FontSize, labelWidth)];
            }

            var top = plotTop + ((category + 0.5) * bandHeight) - (lines.Count * LineHeight / 2);

            for (var index = 0; index < lines.Count; index++)
            {
                page.DrawText(lines[index], Font, FontSize, plotLeft - 6 - Font.Measure(lines[index], FontSize), top + (index * LineHeight), ReportPdfTheme.Muted);
            }
        }

        var groupHeight = bandHeight * GroupShare;
        var slotHeight = _stacked ? groupHeight : groupHeight / _series.Count;

        foreach (var (category, index, from, to) in GetBars())
        {
            var slotTop = plotTop + (category * bandHeight) + ((bandHeight - groupHeight) / 2) + (_stacked ? 0 : index * slotHeight);
            var left = X(Math.Min(from, to));

            page.FillRectangle(left, slotTop + (slotHeight * (1 - BarShare) / 2), X(Math.Max(from, to)) - left, slotHeight * BarShare, _series[index].Color);
        }

        page.DrawLine(X(0), plotTop, X(0), plotBottom, ReportPdfTheme.Muted, 0.75);
    }

    // The bars of every category, from and to the values they span: side by side from zero, or stacked, positive values
    // above zero and negative ones below it. A missing or zero value has no bar.
    private IEnumerable<(int Category, int Series, double From, double To)> GetBars()
    {
        for (var category = 0; category < _categoryCount; category++)
        {
            var positive = 0d;
            var negative = 0d;

            for (var index = 0; index < _series.Count; index++)
            {
                var value = _series[index].Values[category];

                if (!double.IsFinite(value) || value == 0)
                {
                    continue;
                }

                var from = _stacked ? (value > 0 ? positive : negative) : 0;
                var to = from + value;

                if (_stacked)
                {
                    positive = value > 0 ? to : positive;
                    negative = value < 0 ? to : negative;
                }

                yield return (category, index, from, to);
            }
        }
    }

    private void DrawPie(Box box)
    {
        var page = _flow.Page;
        var values = _series[0].Values;
        var total = values.Where(value => double.IsFinite(value) && value > 0).Sum();
        var legendWidth = _chart.ShowLegend ? DrawPieLegend(box) : 0;
        var areaWidth = box.Width - (legendWidth > 0 ? legendWidth + 12 : 0);
        var radius = Math.Max(10, (Math.Min(areaWidth, box.Height) / 2) - 18);
        var innerRadius = _chart.Type == ReportChartType.Doughnut ? radius / 2 : 0;
        var centerX = box.Left + (areaWidth / 2);
        var centerY = box.Top + (box.Height / 2);
        var slices = new List<(double Start, double Sweep, double Share)>();
        var angle = 0d;

        // Slices go clockwise from twelve o'clock, as on screen.
        for (var category = 0; category < values.Length; category++)
        {
            if (!double.IsFinite(values[category]) || values[category] <= 0)
            {
                continue;
            }

            var share = values[category] / total;
            var sweep = share * 2 * Math.PI;

            page.FillWedge(centerX, centerY, radius, innerRadius, angle, sweep, ReportPdfTheme.GetPaletteColor(category));
            slices.Add((angle, sweep, share));
            angle += sweep;
        }

        if (slices.Count > 1)
        {
            foreach (var (start, _, _) in slices)
            {
                page.DrawLine(
                    centerX + (innerRadius * Math.Sin(start)),
                    centerY - (innerRadius * Math.Cos(start)),
                    centerX + (radius * Math.Sin(start)),
                    centerY - (radius * Math.Cos(start)),
                    ReportPdfTheme.White,
                    0.75);
            }
        }

        // The share of each slice is written outside it; a sliver too thin to point at is left unlabeled.
        var lineHeight = Font.LineHeight * PieLabelSize;

        foreach (var (start, sweep, share) in slices.Where(slice => slice.Share >= 0.02))
        {
            var text = Font.Prepare(share.ToString("0%", CultureInfo.CurrentCulture));
            var width = Font.Measure(text, PieLabelSize);
            var middle = start + (sweep / 2);
            var distance = radius + 4 + (Math.Max(width, lineHeight) / 2);
            var x = centerX + (distance * Math.Sin(middle));
            var y = centerY - (distance * Math.Cos(middle));

            page.DrawText(text, Font, PieLabelSize, x - (width / 2), y - (lineHeight / 2), ReportPdfTheme.Text);
        }
    }

    private double DrawPieLegend(Box box)
    {
        // The slices of a pie are named in a column at its right.
        var maxWidth = box.Width * 0.4;
        var rowHeight = LineHeight * 1.3;
        var items = _labels.Select((label, index) => (Text: ReportPdfText.Fit(label, Font, FontSize, maxWidth - Swatch - 3), Color: ReportPdfTheme.GetPaletteColor(index))).ToList();
        var width = Math.Min(maxWidth, items.Max(item => Swatch + 3 + Font.Measure(item.Text, FontSize)));
        var visible = Math.Max(1, Math.Min(items.Count, (int)Math.Floor(box.Height / rowHeight)));
        var left = box.Right - width;
        var top = box.Top + ((box.Height - (visible * rowHeight)) / 2);

        for (var index = 0; index < visible; index++)
        {
            DrawLegendItem(items[index].Text, items[index].Color, left, top + (index * rowHeight));
        }

        return width;
    }

    private double DrawSeriesLegend(Box box)
    {
        if (!_chart.ShowLegend || _series.Count < 2)
        {
            return 0;
        }

        // The series are named in rows under the chart, centered, wrapping when they do not fit across it.
        const double Gap = 12;

        var current = new List<(string Text, ReportPdfColor Color, double Width)>();
        var rows = new List<List<(string Text, ReportPdfColor Color, double Width)>> { current };
        var rowWidth = 0d;

        foreach (var series in _series)
        {
            var text = ReportPdfText.Fit(series.Label, Font, FontSize, box.Width - Swatch - 3);
            var width = Swatch + 3 + Font.Measure(text, FontSize);

            if (current.Count > 0 && rowWidth + Gap + width > box.Width)
            {
                current = [];
                rows.Add(current);
                rowWidth = 0;
            }

            rowWidth += (current.Count > 0 ? Gap : 0) + width;
            current.Add((text, series.Color, width));
        }

        var rowHeight = LineHeight * 1.3;
        var height = (rows.Count * rowHeight) + 4;
        var top = box.Bottom - (rows.Count * rowHeight);

        foreach (var row in rows)
        {
            var x = box.Left + ((box.Width - (row.Sum(item => item.Width) + (Gap * (row.Count - 1)))) / 2);

            foreach (var (text, color, width) in row)
            {
                DrawLegendItem(text, color, x, top);
                x += width + Gap;
            }

            top += rowHeight;
        }

        return height;
    }

    private void DrawLegendItem(string text, ReportPdfColor color, double left, double top)
    {
        _flow.Page.FillRectangle(left, top + ((LineHeight - Swatch) / 2), Swatch, Swatch, color);
        _flow.Page.DrawText(text, Font, FontSize, left + Swatch + 3, top, ReportPdfTheme.Text);
    }

    private int GetLabelStep(double bandWidth)
    {
        var widest = _labels.Max(label => Math.Min(MaxCategoryLabelWidth, Font.Measure(label, FontSize)));

        return Math.Max(1, (int)Math.Ceiling((widest + 4) / bandWidth));
    }

    private Scale GetScale()
    {
        var values = _series.SelectMany(series => series.Values).Where(double.IsFinite).ToList();

        // Stacked bars are read against the totals of their categories.
        if (_stacked)
        {
            var totals = Enumerable.Range(0, _categoryCount).SelectMany(category => new[]
            {
                _series.Select(series => series.Values[category]).Where(value => double.IsFinite(value) && value > 0).Sum(),
                _series.Select(series => series.Values[category]).Where(value => double.IsFinite(value) && value < 0).Sum(),
            });

            values = [.. values, .. totals];
        }

        return Scale.Create(values, _integers, _chart.PercentageScale);
    }

    private string FormatTick(double value)
    {
        var format = _integers ? "#,##0" : "#,##0.##";

        return Font.Prepare(value.ToString(_chart.PercentageScale ? format + "'%'" : format, CultureInfo.CurrentCulture));
    }

    // Puts a line of text so that the middle of its digits is at a height, such as the height of a gridline.
    private double CenteredTop(double y)
    {
        return y - (Font.Ascent * FontSize) + (FontSize * 0.35);
    }

    private string SingleLine(string value)
    {
        return Font.Prepare(value).Replace('\n', ' ');
    }

    private sealed record Series(string Label, double[] Values, ReportPdfColor Color);

    private readonly record struct Box(double Left, double Top, double Width, double Height)
    {
        public double Right => Left + Width;

        public double Bottom => Top + Height;
    }

    /// <summary>
    /// The value axis of a chart: from zero, or below it, to a round number past the largest value, in round steps.
    /// </summary>
    private sealed class Scale
    {
        private Scale(
            double min,
            double max,
            double step)
        {
            Min = min;
            Max = max;

            for (var index = 0; min + (index * step) <= max + (step / 2); index++)
            {
                Ticks.Add(Math.Round(min + (index * step), 10));
            }
        }

        public double Min { get; }

        public double Max { get; }

        public List<double> Ticks { get; } = [];

        public static Scale Create(List<double> values, bool integers, bool percentage)
        {
            // Percentages are read against the whole scale, as on screen.
            if (percentage && values.Count > 0 && values.Min() >= 0 && values.Max() <= 100)
            {
                return new Scale(0, 100, 10);
            }

            var min = Math.Min(0, values.Count == 0 ? 0 : values.Min());
            var max = Math.Max(0, values.Count == 0 ? 0 : values.Max());

            if (max - min < 1e-12)
            {
                max = min + 1;
            }

            var step = NiceNumber((max - min) / 5);

            // Counts are not divided into fractions.
            if (integers)
            {
                step = Math.Max(1, step);
            }

            return new Scale(Math.Floor((min / step) + 1e-9) * step, Math.Ceiling((max / step) - 1e-9) * step, step);
        }

        private static double NiceNumber(double value)
        {
            var exponent = Math.Floor(Math.Log10(value));
            var fraction = value / Math.Pow(10, exponent);
            var nice = fraction switch
            {
                < 1.5 => 1,
                < 3 => 2,
                < 7 => 5,
                _ => 10,
            };

            return nice * Math.Pow(10, exponent);
        }
    }
}
