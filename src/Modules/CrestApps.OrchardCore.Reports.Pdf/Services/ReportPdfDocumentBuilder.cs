using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Reports.Models;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Shapes.Charts;
using MigraDoc.DocumentObjectModel.Tables;
using PdfSharp.Drawing;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Lays a report document out as a MigraDoc document. Sections are stacked at the full width of the page, in order, and
/// the sections of a container follow its title band, as on the page.
/// </summary>
internal sealed class ReportPdfDocumentBuilder
{
    private const string FontFamily = "Arial";
    private const double BaseFontSize = 9;
    private const double Margin = 40;
    private const int MetricsPerRow = 4;

    // A table of more columns than this is laid out in landscape when its values do not fit on one line in portrait.
    private const int PortraitColumns = 6;

    // The colors of the report on screen: Bootstrap's body text, muted text, borders and table states, and the
    // palette of the report charts.
    private static readonly Color _text = Color.FromRgb(0x21, 0x25, 0x29);
    private static readonly Color _muted = Color.FromRgb(0x6C, 0x75, 0x7D);
    private static readonly Color _border = Color.FromRgb(0xDE, 0xE2, 0xE6);
    private static readonly Color _headerFill = Color.FromRgb(0xF1, 0xF3, 0xF5);
    private static readonly Color _cardFill = Color.FromRgb(0xF8, 0xF9, 0xFA);
    private static readonly Color _subtotalFill = Color.FromRgb(0xCF, 0xF4, 0xFC);
    private static readonly Color _grandTotalFill = Color.FromRgb(0xCF, 0xE2, 0xFF);
    private static readonly Color _barTrack = Color.FromRgb(0xE9, 0xEC, 0xEF);
    private static readonly Color _white = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color _primary = Color.FromRgb(0x0D, 0x6E, 0xFD);

    private static readonly Color[] _palette =
    [
        Color.FromRgb(0x25, 0x63, 0xEB),
        Color.FromRgb(0x14, 0xB8, 0xA6),
        Color.FromRgb(0xF5, 0x9E, 0x0B),
        Color.FromRgb(0xEF, 0x44, 0x44),
        Color.FromRgb(0x8B, 0x5C, 0xF6),
        Color.FromRgb(0x06, 0xB6, 0xD4),
        Color.FromRgb(0x84, 0xCC, 0x16),
        Color.FromRgb(0xF9, 0x73, 0x16),
        Color.FromRgb(0xEC, 0x48, 0x99),
        Color.FromRgb(0x64, 0x74, 0x8B),
    ];

    private readonly ReportDocument _report;
    private readonly string _pageNumberFormat;
    private readonly Dictionary<ReportSection, ReportPdfTableLayout> _tables = [];

    private double _usableWidth;
    private double _usableHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportPdfDocumentBuilder"/> class.
    /// </summary>
    /// <param name="report">The report to lay out.</param>
    /// <param name="pageNumberFormat">The page numbers in the footer, with <c>{0}</c> for the page and <c>{1}</c> for the number of pages.</param>
    public ReportPdfDocumentBuilder(ReportDocument report, string pageNumberFormat)
    {
        _report = report;
        _pageNumberFormat = pageNumberFormat;
    }

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <returns>The MigraDoc document.</returns>
    public Document Build()
    {
        var document = new Document();

        if (!string.IsNullOrWhiteSpace(_report.Title))
        {
            document.Info.Title = _report.Title;
        }

        DefineStyles(document);
        MeasureTables();

        var section = document.AddSection();

        SetUpPage(section);
        AddFooter(section);

        if (!string.IsNullOrWhiteSpace(_report.Title))
        {
            AddText(section.AddParagraph(), _report.Title).Style = "ReportTitle";
        }

        var first = true;

        foreach (var group in _report.GetSectionGroups())
        {
            if (!string.IsNullOrWhiteSpace(group.Container?.Title))
            {
                AddText(section.AddParagraph(), group.Container.Title).Style = "ContainerTitle";
            }

            foreach (var reportSection in group.Sections)
            {
                AddSection(section, reportSection, first);
                first = false;
            }
        }

        return document;
    }

    private void AddSection(Section section, ReportSection reportSection, bool first)
    {
        // The first section is often named like the report; its title is not repeated under the report's.
        if (!string.IsNullOrWhiteSpace(reportSection.Title) &&
            !(first && string.Equals(reportSection.Title, _report.Title, StringComparison.OrdinalIgnoreCase)))
        {
            AddText(section.AddParagraph(), reportSection.Title).Style = "SectionTitle";
        }

        // Which values picked in other sections filter this one, as those sections show them.
        foreach (var filter in _report.GetFilteringSections(reportSection))
        {
            var values = string.Join(", ", filter.Selection.GetPickedValues());
            var text = string.IsNullOrWhiteSpace(filter.Title) ? $"Filtered by: {values}" : $"Filtered by {filter.Title}: {values}";

            AddText(section.AddParagraph(), text).Style = "SectionDescription";
        }

        if (!string.IsNullOrWhiteSpace(reportSection.Description))
        {
            AddText(section.AddParagraph(), reportSection.Description).Style = "SectionDescription";
        }

        switch (reportSection.Kind)
        {
            case ReportSectionKind.Metrics:
                AddMetrics(section, reportSection.Metrics);
                break;
            case ReportSectionKind.Table:
                AddTable(section, reportSection);
                break;
            case ReportSectionKind.Bars:
                AddBars(section, reportSection.Bars);
                break;
            case ReportSectionKind.Chart:
                AddChart(section, reportSection.Chart);
                break;
        }
    }

    private static void DefineStyles(Document document)
    {
        var normal = document.Styles[StyleNames.Normal];
        normal.Font.Name = FontFamily;
        normal.Font.Size = BaseFontSize;
        normal.Font.Color = _text;

        var title = document.Styles.AddStyle("ReportTitle", StyleNames.Normal);
        title.Font.Size = 18;
        title.Font.Bold = true;
        title.ParagraphFormat.SpaceAfter = 8;

        // A container's title band, colored as on the page.
        var containerTitle = document.Styles.AddStyle("ContainerTitle", StyleNames.Normal);
        containerTitle.Font.Size = 11;
        containerTitle.Font.Bold = true;
        containerTitle.Font.Color = _white;
        containerTitle.ParagraphFormat.Shading.Color = _primary;
        containerTitle.ParagraphFormat.Borders.Distance = 5;
        containerTitle.ParagraphFormat.Borders.Color = _primary;
        containerTitle.ParagraphFormat.SpaceBefore = 18;
        containerTitle.ParagraphFormat.SpaceAfter = 6;
        containerTitle.ParagraphFormat.KeepWithNext = true;

        var sectionTitle = document.Styles.AddStyle("SectionTitle", StyleNames.Normal);
        sectionTitle.Font.Size = 12;
        sectionTitle.Font.Bold = true;
        sectionTitle.ParagraphFormat.SpaceBefore = 14;
        sectionTitle.ParagraphFormat.SpaceAfter = 6;
        sectionTitle.ParagraphFormat.KeepWithNext = true;

        var description = document.Styles.AddStyle("SectionDescription", StyleNames.Normal);
        description.Font.Size = BaseFontSize - 0.5;
        description.Font.Color = _muted;
        description.ParagraphFormat.SpaceAfter = 6;
        description.ParagraphFormat.KeepWithNext = true;

        var footer = document.Styles[StyleNames.Footer];
        footer.Font.Size = BaseFontSize - 1.5;
        footer.Font.Color = _muted;
        footer.ParagraphFormat.Alignment = ParagraphAlignment.Right;
    }

    private void MeasureTables()
    {
        using var graphics = XGraphics.CreateMeasureContext(new XSize(2000, 2000), XGraphicsUnit.Point, XPageDirection.Downwards);

        foreach (var reportSection in _report.Sections)
        {
            if (reportSection?.Kind == ReportSectionKind.Table &&
                ReportPdfTableLayout.Measure(reportSection, graphics, FontFamily, BaseFontSize) is { } layout)
            {
                _tables[reportSection] = layout;
            }
        }
    }

    private void SetUpPage(Section section)
    {
        PageSetup.GetPageSize(PageFormat.Letter, out var width, out var height);

        // Landscape when a table's words do not fit across a portrait page, or a table of many columns would wrap.
        var portraitWidth = width.Point - (2 * Margin);
        var landscape = _tables.Values.Any(layout =>
            layout.MinimumWidth > portraitWidth ||
            (layout.ColumnCount > PortraitColumns && layout.PreferredWidth > portraitWidth));
        var pageWidth = landscape ? height.Point : width.Point;
        var pageHeight = landscape ? width.Point : height.Point;

        // The page is sized by its width and height in the direction it is read: MigraDoc's default ones, of an A4
        // page, win over a page format, and it does not turn a page whose size is set.
        section.PageSetup = section.Document.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.Letter;
        section.PageSetup.PageWidth = Unit.FromPoint(pageWidth);
        section.PageSetup.PageHeight = Unit.FromPoint(pageHeight);
        section.PageSetup.Orientation = Orientation.Portrait;
        section.PageSetup.TopMargin = Margin;
        section.PageSetup.BottomMargin = Margin + 10;
        section.PageSetup.LeftMargin = Margin;
        section.PageSetup.RightMargin = Margin;
        section.PageSetup.FooterDistance = Margin / 2;

        _usableWidth = pageWidth - (2 * Margin);
        _usableHeight = pageHeight - (2 * Margin) - 10;
    }

    private void AddFooter(Section section)
    {
        var paragraph = section.Footers.Primary.AddParagraph();

        // The page numbers are fields, written where the localized text puts {0} and {1}, in whatever order.
        var format = string.IsNullOrEmpty(_pageNumberFormat) ? "{0} / {1}" : _pageNumberFormat;
        var position = 0;

        while (position < format.Length)
        {
            var page = format.IndexOf("{0}", position, StringComparison.Ordinal);
            var pages = format.IndexOf("{1}", position, StringComparison.Ordinal);
            var next = page < 0 ? pages : pages < 0 ? page : Math.Min(page, pages);

            if (next < 0)
            {
                paragraph.AddText(format[position..]);

                break;
            }

            if (next > position)
            {
                paragraph.AddText(format[position..next]);
            }

            if (next == page)
            {
                paragraph.AddPageField();
            }
            else
            {
                paragraph.AddNumPagesField();
            }

            position = next + 3;
        }
    }

    private void AddMetrics(Section section, IList<ReportMetric> metrics)
    {
        var items = (metrics ?? []).Where(metric => metric is not null).ToList();

        if (items.Count == 0)
        {
            return;
        }

        var columns = Math.Min(items.Count, MetricsPerRow);
        var table = section.AddTable();

        // White borders between the cards keep them apart, like the gutters of the cards on screen.
        table.Borders.Color = _white;
        table.Borders.Width = 4;
        table.TopPadding = 6;
        table.BottomPadding = 6;
        table.LeftPadding = 8;
        table.RightPadding = 8;

        for (var index = 0; index < columns; index++)
        {
            table.AddColumn();
        }

        SetColumnWidths(table, Enumerable.Repeat(1d, columns).ToList());

        Row row = null;

        for (var index = 0; index < items.Count; index++)
        {
            if (index % columns == 0)
            {
                row = table.AddRow();
            }

            var metric = items[index];
            var cell = row.Cells[index % columns];
            cell.Shading.Color = _cardFill;

            var label = AddText(cell.AddParagraph(), metric.Label?.ToUpper(CultureInfo.CurrentCulture));
            label.Format.Font.Size = BaseFontSize - 1.5;
            label.Format.Font.Color = _muted;

            var value = AddText(cell.AddParagraph(), metric.Value);
            // A long value, such as an amount in millions, is set smaller so it stays on its card.
            value.Format.Font.Size = (metric.Value?.Length ?? 0) switch
            {
                <= 10 => 16,
                <= 16 => 12,
                _ => 10,
            };
            value.Format.Font.Bold = true;
            value.Format.SpaceBefore = 2;

            if (!string.IsNullOrWhiteSpace(metric.Hint))
            {
                var hint = AddText(cell.AddParagraph(), metric.Hint);
                hint.Format.Font.Size = BaseFontSize - 1.5;
                hint.Format.Font.Color = _muted;
                hint.Format.SpaceBefore = 2;
            }
        }

        AddSpacer(section);
    }

    private void AddTable(Section section, ReportSection reportSection)
    {
        if (!_tables.TryGetValue(reportSection, out var layout))
        {
            return;
        }

        var columns = reportSection.Columns ?? [];
        var rows = (reportSection.Rows ?? []).Where(row => row is not null).ToList();
        var columnCount = layout.ColumnCount;
        var widths = layout.GetColumnWidths(_usableWidth);
        var charactersPerLine = layout.GetCharactersPerLine(widths);

        var table = section.AddTable();
        table.Format.Font.Size = layout.FontSize;
        table.TopPadding = 3;
        table.BottomPadding = 3;
        table.LeftPadding = ReportPdfTableLayout.CellPadding / 2;
        table.RightPadding = ReportPdfTableLayout.CellPadding / 2;
        table.Borders.Bottom.Color = _border;
        table.Borders.Bottom.Width = 0.5;

        for (var index = 0; index < columnCount; index++)
        {
            var column = table.AddColumn(Unit.FromPoint(widths[index]));
            column.Format.Alignment = ToAlignment(index < columns.Count ? columns[index]?.Align : null);
        }

        if (columns.Count > 0)
        {
            var header = table.AddRow();
            header.HeadingFormat = true;
            header.Format.Font.Bold = true;
            header.Shading.Color = _headerFill;
            header.Borders.Bottom.Color = _muted;
            header.Borders.Bottom.Width = 0.75;

            for (var index = 0; index < columns.Count; index++)
            {
                var cell = header.Cells[index];

                AddText(cell.AddParagraph(), BreakLongWords(columns[index]?.Label, charactersPerLine[index]));
                ApplyStyle(cell, columns[index]?.HeaderStyle);
            }
        }

        foreach (var row in rows)
        {
            var tableRow = table.AddRow();

            if (row.Kind != ReportRowKind.Detail)
            {
                tableRow.Format.Font.Bold = true;
                tableRow.Shading.Color = row.Kind == ReportRowKind.Subtotal ? _subtotalFill : _grandTotalFill;
                tableRow.Borders.Top.Color = _muted;
                tableRow.Borders.Top.Width = 0.75;
            }

            var cells = row.Cells ?? [];

            for (var index = 0; index < Math.Min(cells.Count, columnCount); index++)
            {
                var cell = tableRow.Cells[index];

                AddText(cell.AddParagraph(), BreakLongWords(cells[index], charactersPerLine[index]));
                ApplyStyle(cell, row.GetCellStyle(index));
            }
        }

        AddSpacer(section);
    }

    private void AddBars(Section section, IList<ReportBar> bars)
    {
        var items = (bars ?? []).Where(bar => bar is not null).ToList();

        if (items.Count == 0)
        {
            return;
        }

        foreach (var bar in items)
        {
            // Each bar is a small table of its own: the label and value above, and below them the filled and empty
            // parts of the bar as two cells whose widths follow the ratio, like the progress bars on screen.
            var ratio = double.IsFinite(bar.Ratio) ? Math.Clamp(bar.Ratio, 0, 1) : 0;
            var table = section.AddTable();
            table.Borders.Width = 0;
            table.LeftPadding = 0;
            table.RightPadding = 0;
            table.TopPadding = 0;
            table.BottomPadding = 0;

            var filled = ratio >= 0.995 ? 1 : ratio <= 0.005 ? 0 : ratio;
            var parts = filled is 0 or 1 ? 1 : 2;

            for (var index = 0; index < parts; index++)
            {
                table.AddColumn();
            }

            SetColumnWidths(table, parts == 1 ? [1d] : [filled, 1 - filled]);

            var text = table.AddRow();
            text.KeepWith = 1;
            text.BottomPadding = 2;

            var labelCell = text.Cells[0];
            labelCell.MergeRight = parts - 1;

            // A tab stop at the right edge puts the value at the end of the line the label starts.
            var line = labelCell.AddParagraph();
            line.Format.Font.Size = BaseFontSize - 0.5;
            line.Format.TabStops.AddTabStop(Unit.FromPoint(GetTableWidth(table)), TabAlignment.Right);
            AddText(line, bar.Label);
            line.AddTab();
            line.AddFormattedText(Clean(bar.Value)?.Replace('\n', ' ') ?? string.Empty, TextFormat.Bold);

            var track = table.AddRow();
            track.Height = 6;
            track.HeightRule = RowHeightRule.Exactly;
            track.Format.Font.Size = 1;

            for (var index = 0; index < parts; index++)
            {
                track.Cells[index].Shading.Color = index == 0 && filled > 0 ? _palette[0] : _barTrack;
            }

            var spacer = section.AddParagraph();
            spacer.Format.Font.Size = 1;
            spacer.Format.SpaceAfter = 5;
        }

        AddSpacer(section);
    }

    private void AddChart(Section section, ReportChart reportChart)
    {
        if (reportChart is null)
        {
            return;
        }

        var labels = reportChart.Labels ?? [];
        var datasets = (reportChart.Datasets ?? []).Where(dataset => dataset?.Values is { Count: > 0 }).ToList();

        if (datasets.Count == 0)
        {
            return;
        }

        var categoryCount = Math.Max(labels.Count, datasets.Max(dataset => dataset.Values.Count));
        var type = ToChartType(reportChart);
        var isPie = type == ChartType.Pie2D;
        var isHorizontal = type is ChartType.Bar2D or ChartType.BarStacked2D;

        if (isPie)
        {
            // A pie shows one series, as the doughnuts and pies on screen do.
            datasets = [datasets[0]];
        }

        // MigraDoc draws the first category of sideways bars at the bottom; on screen it is at the top.
        int[] order = isHorizontal
            ? [.. Enumerable.Range(0, categoryCount).Reverse()]
            : [.. Enumerable.Range(0, categoryCount)];

        var height = isPie
            ? 260
            : isHorizontal
                ? Math.Clamp(categoryCount * ((datasets.Count * 12) + 8) + 60, 180, _usableHeight * 0.9)
                : 240;

        var chart = new Chart(type)
        {
            Width = Unit.FromPoint(_usableWidth),
            Height = Unit.FromPoint(height),
            Left = ShapePosition.Center,
        };

        chart.Format.Font.Name = FontFamily;
        chart.Format.Font.Size = BaseFontSize - 2;
        chart.Format.Font.Color = _text;

        for (var datasetIndex = 0; datasetIndex < datasets.Count; datasetIndex++)
        {
            var dataset = datasets[datasetIndex];
            var series = chart.SeriesCollection.AddSeries();
            var color = _palette[datasetIndex % _palette.Length];

            series.Name = Clean(dataset.Label) ?? string.Empty;

            foreach (var index in order)
            {
                var value = index < dataset.Values.Count ? dataset.Values[index] : double.NaN;

                if (!double.IsFinite(value))
                {
                    series.AddBlank();

                    continue;
                }

                var point = series.Add(value);

                if (isPie)
                {
                    point.FillFormat.Color = _palette[index % _palette.Length];
                    point.LineFormat.Color = _white;
                    point.LineFormat.Width = 0.75;
                }
            }

            if (type == ChartType.Line)
            {
                series.LineFormat.Color = color;
                series.LineFormat.Width = 2;
                series.MarkerStyle = MarkerStyle.Circle;
                series.MarkerSize = Unit.FromPoint(4);
                series.MarkerBackgroundColor = color;
                series.MarkerForegroundColor = color;
            }
            else if (!isPie)
            {
                series.FillFormat.Color = color;
                series.LineFormat.Color = color;
                series.LineFormat.Visible = false;
            }
        }

        var categories = chart.XValues.AddXSeries();

        foreach (var index in order)
        {
            categories.Add(index < labels.Count ? Clean(labels[index]) ?? string.Empty : string.Empty);
        }

        if (!isPie)
        {
            var values = datasets.SelectMany(dataset => dataset.Values).Where(double.IsFinite).ToList();
            var whole = values.All(value => Math.Abs(value - Math.Round(value)) < 1e-9);
            var format = whole ? "#,##0" : "#,##0.##";

            chart.XAxis.MajorTickMark = TickMarkType.Outside;
            chart.XAxis.LineFormat.Color = _border;
            chart.YAxis.MajorTickMark = TickMarkType.Outside;
            chart.YAxis.HasMajorGridlines = true;
            chart.YAxis.MajorGridlines.LineFormat.Color = _border;
            chart.YAxis.MajorGridlines.LineFormat.Width = 0.5;
            chart.YAxis.LineFormat.Visible = false;
            chart.YAxis.TickLabels.Format = reportChart.PercentageScale ? format + "'%'" : format;
            chart.PlotArea.LineFormat.Visible = false;

            // Percentages are read against the whole scale, as on screen. The tick is set too: MigraDoc keeps the one
            // it chose for the values, which crowds a scale it did not choose.
            if (reportChart.PercentageScale && values.Count > 0 && values.Min() >= 0 && values.Max() <= 100)
            {
                chart.YAxis.MinimumScale = 0;
                chart.YAxis.MaximumScale = 100;
                chart.YAxis.MajorTick = 10;
            }
            else if (whole && values.Count > 0)
            {
                // Whole numbers are read on whole steps: MigraDoc picks half steps for small values, which the format
                // rounds into repeated labels.
                // Stacked marks reach the sums of their category's values.
                var stacked = type is ChartType.ColumnStacked2D or ChartType.BarStacked2D;
                var reach = stacked
                    ? Enumerable.Range(0, categoryCount)
                        .SelectMany(index => new[]
                        {
                            datasets.Sum(dataset => index < dataset.Values.Count && double.IsFinite(dataset.Values[index]) ? Math.Max(0, dataset.Values[index]) : 0),
                            datasets.Sum(dataset => index < dataset.Values.Count && double.IsFinite(dataset.Values[index]) ? Math.Min(0, dataset.Values[index]) : 0),
                        })
                        .ToList()
                    : values;
                var low = Math.Min(0, reach.Min());
                var high = Math.Max(0, reach.Max());
                var step = WholeStep(high - low);

                chart.YAxis.MinimumScale = Math.Floor(low / step) * step;
                chart.YAxis.MaximumScale = Math.Max(Math.Ceiling(high / step) * step, chart.YAxis.MinimumScale + step);
                chart.YAxis.MajorTick = step;
            }
        }

        if (reportChart.ShowLegend && (isPie || datasets.Count > 1))
        {
            var legend = isPie
                ? chart.RightArea.AddLegend()
                : chart.BottomArea.AddLegend();

            legend.Format.Font.Size = BaseFontSize - 2;
        }

        if (isPie)
        {
            chart.DataLabel.Type = DataLabelType.Percent;
            chart.DataLabel.Position = DataLabelPosition.OutsideEnd;
            chart.DataLabel.Font.Size = BaseFontSize - 2.5;
        }

        // MigraDoc draws a chart after the paragraph before it; a spacer keeps it off the section title.
        var holder = section.AddParagraph();
        holder.Format.SpaceBefore = 4;
        holder.Format.KeepWithNext = true;

        section.Add(chart);

        AddSpacer(section);
    }

    // A whole step of 1, 2 or 5 times a power of ten that splits a range into at most ten ticks.
    private static double WholeStep(double range)
    {
        if (range <= 10)
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(range / 10)));

        foreach (var factor in (double[])[1, 2, 5, 10])
        {
            if (range / (factor * magnitude) <= 10)
            {
                return factor * magnitude;
            }
        }

        return 10 * magnitude;
    }

    private static ChartType ToChartType(ReportChart chart)
    {
        return chart.Type switch
        {
            ReportChartType.Line => ChartType.Line,
            ReportChartType.Area => ChartType.Area2D,
            ReportChartType.Pie or ReportChartType.Doughnut => ChartType.Pie2D,
            ReportChartType.HorizontalBar => chart.Stacked ? ChartType.BarStacked2D : ChartType.Bar2D,
            _ => chart.Stacked ? ChartType.ColumnStacked2D : ChartType.Column2D,
        };
    }

    private void SetColumnWidths(Table table, List<double> weights)
    {
        var total = weights.Sum();

        for (var index = 0; index < table.Columns.Count; index++)
        {
            table.Columns[index].Width = Unit.FromPoint(_usableWidth * weights[index] / total);
        }
    }

    private static double GetTableWidth(Table table)
    {
        var width = 0d;

        for (var index = 0; index < table.Columns.Count; index++)
        {
            width += table.Columns[index].Width.Point;
        }

        return width;
    }

    private static void ApplyStyle(Cell cell, ReportStyle style)
    {
        if (style is null || style.IsEmpty)
        {
            return;
        }

        if (TryGetColor(style.Color, out var color))
        {
            cell.Format.Font.Color = color;
        }

        if (TryGetColor(style.BackgroundColor, out var background))
        {
            cell.Shading.Color = background;
        }

        if (style.Bold)
        {
            cell.Format.Font.Bold = true;
        }
    }

    private static bool TryGetColor(string value, out Color color)
    {
        color = Color.Empty;

        if (!ReportColor.TryGetArgb(value, out var argb) ||
            !uint.TryParse(argb, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        color = new Color(number);

        return true;
    }

    private static ParagraphAlignment ToAlignment(ReportColumnAlign? align)
    {
        return align switch
        {
            ReportColumnAlign.Center => ParagraphAlignment.Center,
            ReportColumnAlign.End => ParagraphAlignment.Right,
            _ => ParagraphAlignment.Left,
        };
    }

    private static void AddSpacer(Section section)
    {
        var spacer = section.AddParagraph();
        spacer.Format.Font.Size = 1;
        spacer.Format.SpaceAfter = 8;
    }

    private static Paragraph AddText(Paragraph paragraph, string value)
    {
        var text = Clean(value);

        if (string.IsNullOrEmpty(text))
        {
            return paragraph;
        }

        var lines = text.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                paragraph.AddLineBreak();
            }

            if (lines[index].Length > 0)
            {
                paragraph.AddText(lines[index]);
            }
        }

        return paragraph;
    }

    /// <summary>
    /// Breaks the words of a value that are longer than a line of their column, which would otherwise run over the
    /// next column.
    /// </summary>
    private static string BreakLongWords(string value, int charactersPerLine)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= charactersPerLine)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);
        var wordLength = 0;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                wordLength = 0;
            }
            else if (wordLength == charactersPerLine)
            {
                builder.Append('\n');
                wordLength = 1;
            }
            else
            {
                wordLength++;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Keeps the line breaks of a value and turns the other control characters, which a font has no glyph for, into
    /// spaces.
    /// </summary>
    private static string Clean(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (character == '\r')
            {
                if (index + 1 < value.Length && value[index + 1] == '\n')
                {
                    continue;
                }

                builder.Append('\n');
            }
            else if (character == '\n')
            {
                builder.Append('\n');
            }
            else
            {
                builder.Append(char.IsControl(character) ? ' ' : character);
            }
        }

        return builder.ToString();
    }
}
