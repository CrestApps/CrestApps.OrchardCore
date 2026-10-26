using System.Globalization;
using CrestApps.OrchardCore.Reports.Models;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Lays a report document out as a PDF, written with PdfPig. Sections are stacked at the full width of the page, in
/// order, and the title of a section stays on the page of its first content.
/// </summary>
internal sealed class ReportPdfDocumentBuilder
{
    private const double BaseFontSize = ReportPdfTheme.BaseFontSize;
    private const double TitleSize = 18;
    private const double SectionTitleSize = 12;
    private const double DescriptionSize = BaseFontSize - 0.5;
    private const double SmallSize = BaseFontSize - 1.5;
    private const int MetricsPerRow = 4;
    private const double CardGap = 4;
    private const double CardVerticalPadding = 6;
    private const double CardHorizontalPadding = 8;
    private const double BarHeight = 6;
    private const double SectionSpacing = 9;

    // US Letter, in points.
    private const double LetterWidth = 612;
    private const double LetterHeight = 792;

    // A table of more columns than this is laid out in landscape when its values do not fit on one line in portrait.
    private const int PortraitColumns = 6;

    private readonly ReportDocument _report;
    private readonly string _pageNumberFormat;
    private readonly ReportPdfFonts _fonts;
    private readonly Dictionary<ReportSection, ReportPdfTableLayout> _tables = [];

    private ReportPdfFlow _flow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportPdfDocumentBuilder"/> class.
    /// </summary>
    /// <param name="report">The report to lay out.</param>
    /// <param name="pageNumberFormat">The page numbers in the footer, with <c>{0}</c> for the page and <c>{1}</c> for the number of pages.</param>
    /// <param name="fonts">The font files to write the report in.</param>
    public ReportPdfDocumentBuilder(ReportDocument report, string pageNumberFormat, ReportPdfFonts fonts)
    {
        _report = report;
        _pageNumberFormat = pageNumberFormat;
        _fonts = fonts;
    }

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <returns>The PDF file.</returns>
    public byte[] Build()
    {
        using var builder = new PdfDocumentBuilder();

        if (!string.IsNullOrWhiteSpace(_report.Title))
        {
            builder.DocumentInformation.Title = _report.Title;
        }

        var regular = _fonts.CreateRegular(builder);
        var bold = _fonts.CreateBold(builder);

        MeasureTables(bold);

        // Landscape when a table's words do not fit across a portrait page, or a table of many columns would wrap.
        var portraitWidth = LetterWidth - (2 * ReportPdfFlow.Margin);
        var landscape = _tables.Values.Any(layout =>
            layout.MinimumWidth > portraitWidth ||
            (layout.ColumnCount > PortraitColumns && layout.PreferredWidth > portraitWidth));

        _flow = landscape
            ? new ReportPdfFlow(builder, LetterHeight, LetterWidth, regular, bold)
            : new ReportPdfFlow(builder, LetterWidth, LetterHeight, regular, bold);

        if (!string.IsNullOrWhiteSpace(_report.Title))
        {
            WriteParagraph(_report.Title, bold, TitleSize, ReportPdfTheme.Text, keepWith: 0);
            _flow.Advance(8);
        }

        for (var index = 0; index < _report.Sections.Count; index++)
        {
            var section = _report.Sections[index];

            if (section is not null)
            {
                // The first section is often named like the report; its title is not repeated under the report's.
                var showTitle = !string.IsNullOrWhiteSpace(section.Title) &&
                    !(index == 0 && string.Equals(section.Title, _report.Title, StringComparison.OrdinalIgnoreCase));

                WriteSection(section, showTitle);
            }
        }

        _flow.WriteFooters(_pageNumberFormat);

        return builder.Build();
    }

    private void MeasureTables(ReportPdfFont font)
    {
        foreach (var section in _report.Sections)
        {
            if (section?.Kind == ReportSectionKind.Table &&
                ReportPdfTableLayout.Measure(section, font, BaseFontSize) is { } layout)
            {
                _tables[section] = layout;
            }
        }
    }

    private void WriteSection(ReportSection section, bool showTitle)
    {
        var chart = section.Kind == ReportSectionKind.Chart ? new ReportPdfChartWriter(_flow, section.Chart) : null;
        var table = section.Kind == ReportSectionKind.Table && _tables.TryGetValue(section, out var layout)
            ? new ReportPdfTableWriter(_flow, section, layout)
            : null;

        // The title and description of a section stay with the start of its content.
        var lead = section.Kind switch
        {
            ReportSectionKind.Metrics => GetMetricRows(section.Metrics).FirstOrDefault() is { } row ? GetCardsHeight(row) : 0,
            ReportSectionKind.Table => table?.GetLeadHeight() ?? 0,
            ReportSectionKind.Bars => GetBars(section.Bars).FirstOrDefault() is { } bar ? GetBarHeight(bar) : 0,
            ReportSectionKind.Chart => chart.Height,
            _ => 0,
        };

        var description = string.IsNullOrWhiteSpace(section.Description)
            ? []
            : ReportPdfText.Wrap(_flow.Regular.Prepare(section.Description), _flow.Regular, DescriptionSize, _flow.Width);
        var descriptionHeight = description.Count == 0 ? 0 : ReportPdfFlow.GetHeight(description, _flow.Regular, DescriptionSize) + 6;

        if (showTitle)
        {
            _flow.AddSpace(14);
            WriteParagraph(section.Title, _flow.Bold, SectionTitleSize, ReportPdfTheme.Text, keepWith: 6 + descriptionHeight + lead);
            _flow.Advance(6);
        }

        if (description.Count > 0)
        {
            if (!showTitle)
            {
                _flow.EnsureSpace(descriptionHeight + lead);
            }

            _flow.WriteLines(description, _flow.Regular, DescriptionSize, ReportPdfTheme.Muted);
            _flow.Advance(6);
        }

        switch (section.Kind)
        {
            case ReportSectionKind.Metrics:
                WriteMetrics(section.Metrics);
                break;
            case ReportSectionKind.Table:
                if (table is not null)
                {
                    table.Write();
                    _flow.AddSpace(SectionSpacing);
                }

                break;
            case ReportSectionKind.Bars:
                WriteBars(section.Bars);
                break;
            case ReportSectionKind.Chart:
                if (chart.Height > 0)
                {
                    chart.Write();
                    _flow.AddSpace(SectionSpacing);
                }

                break;
        }
    }

    private void WriteParagraph(string value, ReportPdfFont font, double size, ReportPdfColor color, double keepWith)
    {
        var lines = ReportPdfText.Wrap(font.Prepare(value), font, size, _flow.Width);

        _flow.EnsureSpace(ReportPdfFlow.GetHeight(lines, font, size) + keepWith);
        _flow.WriteLines(lines, font, size, color);
    }

    private static List<List<ReportMetric>> GetMetricRows(IList<ReportMetric> metrics)
    {
        return (metrics ?? [])
            .Where(metric => metric is not null)
            .Chunk(MetricsPerRow)
            .Select(row => row.ToList())
            .ToList();
    }

    private void WriteMetrics(IList<ReportMetric> metrics)
    {
        var rows = GetMetricRows(metrics);

        if (rows.Count == 0)
        {
            return;
        }

        // Every row has the columns of the first, so the cards line up, like the cards on screen.
        var columns = rows[0].Count;
        var cardWidth = (_flow.Width - ((columns - 1) * CardGap)) / columns;

        foreach (var row in rows)
        {
            var cards = row.Select(metric => CreateCard(metric, cardWidth)).ToList();
            var height = cards.Max(card => card.Height);

            _flow.EnsureSpace(height);

            for (var index = 0; index < cards.Count; index++)
            {
                var left = _flow.Left + (index * (cardWidth + CardGap));

                _flow.Page.FillRectangle(left, _flow.Top, cardWidth, height, ReportPdfTheme.CardFill);
                DrawCard(cards[index], left + CardHorizontalPadding, _flow.Top + CardVerticalPadding);
            }

            _flow.Advance(height + CardGap);
        }

        _flow.AddSpace(SectionSpacing - CardGap);
    }

    private double GetCardsHeight(List<ReportMetric> row)
    {
        var columns = Math.Min(row.Count, MetricsPerRow);
        var cardWidth = (_flow.Width - ((columns - 1) * CardGap)) / columns;

        return row.Max(metric => CreateCard(metric, cardWidth).Height);
    }

    private Card CreateCard(ReportMetric metric, double cardWidth)
    {
        var width = cardWidth - (2 * CardHorizontalPadding);
        var regular = _flow.Regular;
        var bold = _flow.Bold;

        // A long value, such as an amount in millions, is set smaller so it stays on its card.
        var valueSize = (metric.Value?.Length ?? 0) switch
        {
            <= 10 => 16,
            <= 16 => 12,
            _ => 10,
        };

        var parts = new List<CardPart>
        {
            new(ReportPdfText.Wrap(regular.Prepare(metric.Label?.ToUpper(CultureInfo.CurrentCulture)), regular, SmallSize, width), regular, SmallSize, ReportPdfTheme.Muted, 0),
            new(ReportPdfText.Wrap(bold.Prepare(metric.Value), bold, valueSize, width), bold, valueSize, ReportPdfTheme.Text, 2),
        };

        if (!string.IsNullOrWhiteSpace(metric.Hint))
        {
            parts.Add(new CardPart(ReportPdfText.Wrap(regular.Prepare(metric.Hint), regular, SmallSize, width), regular, SmallSize, ReportPdfTheme.Muted, 2));
        }

        var height = (2 * CardVerticalPadding) + parts.Sum(part => part.SpaceBefore + ReportPdfFlow.GetHeight(part.Lines, part.Font, part.Size));

        return new Card(parts, height);
    }

    private void DrawCard(Card card, double left, double top)
    {
        foreach (var part in card.Parts)
        {
            top += part.SpaceBefore;

            foreach (var line in part.Lines)
            {
                _flow.Page.DrawText(line, part.Font, part.Size, left, top, part.Color);
                top += part.Font.LineHeight * part.Size;
            }
        }
    }

    private static List<ReportBar> GetBars(IList<ReportBar> bars)
    {
        return (bars ?? []).Where(bar => bar is not null).ToList();
    }

    private void WriteBars(IList<ReportBar> bars)
    {
        var items = GetBars(bars);

        if (items.Count == 0)
        {
            return;
        }

        foreach (var bar in items)
        {
            // The label and value above, and below them the filled and empty parts of the bar, whose widths follow the
            // ratio, like the progress bars on screen.
            var (value, lines) = PrepareBar(bar);
            var textHeight = Math.Max(1, lines.Count) * _flow.Regular.LineHeight * DescriptionSize;
            var ratio = double.IsFinite(bar.Ratio) ? Math.Clamp(bar.Ratio, 0, 1) : 0;
            var filled = ratio >= 0.995 ? 1 : ratio <= 0.005 ? 0 : ratio;

            _flow.EnsureSpace(GetBarHeight(bar));

            var top = _flow.Top;

            for (var index = 0; index < lines.Count; index++)
            {
                _flow.Page.DrawText(lines[index], _flow.Regular, DescriptionSize, _flow.Left, top + (index * _flow.Regular.LineHeight * DescriptionSize), ReportPdfTheme.Text);
            }

            _flow.Page.DrawText(value, _flow.Bold, DescriptionSize, _flow.Left + _flow.Width - _flow.Bold.Measure(value, DescriptionSize), top, ReportPdfTheme.Text);

            var trackTop = top + textHeight + 2;

            _flow.Page.FillRectangle(_flow.Left, trackTop, _flow.Width, BarHeight, ReportPdfTheme.BarTrack);
            _flow.Page.FillRectangle(_flow.Left, trackTop, _flow.Width * filled, BarHeight, ReportPdfTheme.Palette[0]);
            _flow.Advance(GetBarHeight(bar) + 6);
        }

        _flow.AddSpace(SectionSpacing);
    }

    private double GetBarHeight(ReportBar bar)
    {
        var (_, lines) = PrepareBar(bar);

        return (Math.Max(1, lines.Count) * _flow.Regular.LineHeight * DescriptionSize) + 2 + BarHeight;
    }

    private (string Value, List<string> Lines) PrepareBar(ReportBar bar)
    {
        // The value ends the first line of the label, which wraps before it.
        var value = _flow.Bold.Prepare(bar.Value).Replace('\n', ' ');
        var width = Math.Max(_flow.Width / 4, _flow.Width - _flow.Bold.Measure(value, DescriptionSize) - 8);

        return (value, ReportPdfText.Wrap(_flow.Regular.Prepare(bar.Label), _flow.Regular, DescriptionSize, width));
    }

    private sealed record CardPart(List<string> Lines, ReportPdfFont Font, double Size, ReportPdfColor Color, double SpaceBefore);

    private sealed record Card(List<CardPart> Parts, double Height);
}
