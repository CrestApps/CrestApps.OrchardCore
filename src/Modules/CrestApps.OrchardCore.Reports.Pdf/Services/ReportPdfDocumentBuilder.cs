using System.Globalization;
using CrestApps.OrchardCore.Reports.Models;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Lays a report document out as a PDF, written with PdfPig. The sections are stacked down the pages in order. The head
/// of a section, its title and description under the title band of its container when it is the container's first, is
/// kept on the page where its content starts, unless the two do not fit on one page.
/// </summary>
internal sealed class ReportPdfDocumentBuilder
{
    private const double BaseFontSize = ReportPdfTheme.BaseFontSize;
    private const double TitleSize = 18;
    private const double SectionTitleSize = 12;
    private const double ContainerTitleSize = 11;
    private const double ContainerTitlePadding = 5;
    private const double DescriptionSize = BaseFontSize - 0.5;
    private const double SmallSize = BaseFontSize - 1.5;
    private const int MetricsPerRow = 4;
    private const double CardGap = 4;
    private const double CardVerticalPadding = 6;
    private const double CardHorizontalPadding = 8;
    private const double BarHeight = 6;
    private const double SectionSpacing = 9;

    // The space above the title band of a container, above the title of a section, and below each part of a head.
    private const double ContainerSpacing = 18;
    private const double TitleSpacing = 14;
    private const double HeadSpacing = 6;

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
    public ReportPdfDocumentBuilder(
        ReportDocument report,
        string pageNumberFormat,
        ReportPdfFonts fonts)
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
            _flow.WriteLines(ReportPdfText.Wrap(bold.Prepare(_report.Title), bold, TitleSize, _flow.Width), bold, TitleSize, ReportPdfTheme.Text);
            _flow.Advance(8);
        }

        var first = true;

        foreach (var group in _report.GetSectionGroups())
        {
            // The title band of a container is drawn in the head of its first section.
            var containerTitle = group.Container?.Title;

            foreach (var section in group.Sections)
            {
                // The first section is often named like the report; its title is not repeated under the report's.
                var showTitle = !string.IsNullOrWhiteSpace(section.Title) &&
                    !(first && string.Equals(section.Title, _report.Title, StringComparison.OrdinalIgnoreCase));

                WriteSection(section, containerTitle, showTitle);
                containerTitle = null;
                first = false;
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

    private void WriteSection(ReportSection section, string containerTitle, bool showTitle)
    {
        var head = CreateHead(section, containerTitle, showTitle);

        // The start of a table or chart is kept to what a page has under the head, so that the two fit on a page.
        var space = _flow.PageContentHeight - head.Height;

        switch (section.Kind)
        {
            case ReportSectionKind.Metrics:
                var rows = CreateMetricRows(section.Metrics);

                WriteHead(head, rows.FirstOrDefault()?.Max(card => card.Height) ?? 0);
                WriteMetrics(rows);
                break;
            case ReportSectionKind.Table:
                var table = _tables.TryGetValue(section, out var layout)
                    ? new ReportPdfTableWriter(_flow, section, layout, space)
                    : null;

                WriteHead(head, table?.LeadHeight ?? 0);

                if (table is not null)
                {
                    table.Write();
                    _flow.AddSpace(SectionSpacing);
                }

                break;
            case ReportSectionKind.Bars:
                var bars = CreateBars(section.Bars);

                WriteHead(head, bars.FirstOrDefault()?.Height ?? 0);
                WriteBars(bars);
                break;
            case ReportSectionKind.Chart:
                var chart = new ReportPdfChartWriter(_flow, section.Chart, space);

                WriteHead(head, chart.Height);

                if (chart.Height > 0)
                {
                    chart.Write();
                    _flow.AddSpace(SectionSpacing);
                }

                break;
            default:
                WriteHead(head, 0);
                break;
        }
    }

    private Head CreateHead(ReportSection section, string containerTitle, bool showTitle)
    {
        var bold = _flow.Bold;
        var regular = _flow.Regular;
        var band = string.IsNullOrWhiteSpace(containerTitle)
            ? []
            : ReportPdfText.Wrap(bold.Prepare(containerTitle), bold, ContainerTitleSize, _flow.Width - (2 * ContainerTitlePadding));
        var title = showTitle
            ? ReportPdfText.Wrap(bold.Prepare(section.Title), bold, SectionTitleSize, _flow.Width)
            : [];

        // Which values picked in other sections filter this one, as those sections show them, then the description.
        var description = new List<string>();

        foreach (var filter in _report.GetFilteringSections(section))
        {
            var values = string.Join(", ", filter.Selection.GetPickedValues());
            var text = string.IsNullOrWhiteSpace(filter.Title) ? $"Filtered by: {values}" : $"Filtered by {filter.Title}: {values}";

            description.AddRange(ReportPdfText.Wrap(regular.Prepare(text), regular, DescriptionSize, _flow.Width));
        }

        if (!string.IsNullOrWhiteSpace(section.Description))
        {
            description.AddRange(ReportPdfText.Wrap(regular.Prepare(section.Description), regular, DescriptionSize, _flow.Width));
        }

        // The height as WriteHead draws it, without the space above the head, which a page does not start with.
        var height = 0d;

        if (band.Count > 0)
        {
            height += GetBandHeight(band) + HeadSpacing;
        }

        if (band.Count > 0 && title.Count > 0)
        {
            height += TitleSpacing;
        }

        if (title.Count > 0)
        {
            height += ReportPdfFlow.GetHeight(title, bold, SectionTitleSize) + HeadSpacing;
        }

        if (description.Count > 0)
        {
            height += ReportPdfFlow.GetHeight(description, regular, DescriptionSize) + HeadSpacing;
        }

        return new Head(band, title, description, height);
    }

    // The head is drawn without a break once there is room for it and the start of the content, so that none of it is
    // left at the bottom of a page. Only a head taller than a page breaks, between its lines.
    private void WriteHead(Head head, double lead)
    {
        var bold = _flow.Bold;

        if (head.Band.Count > 0)
        {
            _flow.AddSpace(ContainerSpacing);
        }
        else if (head.Title.Count > 0)
        {
            _flow.AddSpace(TitleSpacing);
        }

        _flow.EnsureSpace(head.Height + lead);

        // The title band of the container, colored as on the page.
        if (head.Band.Count > 0)
        {
            var height = GetBandHeight(head.Band);
            var lineHeight = bold.LineHeight * ContainerTitleSize;

            _flow.Page.FillRectangle(_flow.Left, _flow.Top, _flow.Width, height, ReportPdfTheme.Primary);

            for (var index = 0; index < head.Band.Count; index++)
            {
                _flow.Page.DrawText(head.Band[index], bold, ContainerTitleSize, _flow.Left + ContainerTitlePadding, _flow.Top + ContainerTitlePadding + (index * lineHeight), ReportPdfTheme.White);
            }

            _flow.Advance(height + HeadSpacing);
        }

        if (head.Band.Count > 0 && head.Title.Count > 0)
        {
            _flow.Advance(TitleSpacing);
        }

        if (head.Title.Count > 0)
        {
            _flow.WriteLines(head.Title, bold, SectionTitleSize, ReportPdfTheme.Text);
            _flow.Advance(HeadSpacing);
        }

        if (head.Description.Count > 0)
        {
            _flow.WriteLines(head.Description, _flow.Regular, DescriptionSize, ReportPdfTheme.Muted);
            _flow.Advance(HeadSpacing);
        }
    }

    private double GetBandHeight(List<string> lines)
    {
        return ReportPdfFlow.GetHeight(lines, _flow.Bold, ContainerTitleSize) + (2 * ContainerTitlePadding);
    }

    private List<List<Card>> CreateMetricRows(IList<ReportMetric> metrics)
    {
        var rows = (metrics ?? [])
            .Where(metric => metric is not null)
            .Chunk(MetricsPerRow)
            .ToList();

        if (rows.Count == 0)
        {
            return [];
        }

        // Every row has the columns of the first, so the cards line up, like the cards on screen.
        var columns = rows[0].Length;
        var cardWidth = (_flow.Width - ((columns - 1) * CardGap)) / columns;

        return rows
            .Select(row => row.Select(metric => CreateCard(metric, cardWidth)).ToList())
            .ToList();
    }

    private void WriteMetrics(List<List<Card>> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var cards in rows)
        {
            var height = cards.Max(card => card.Height);

            _flow.EnsureSpace(height);

            for (var index = 0; index < cards.Count; index++)
            {
                var left = _flow.Left + (index * (cards[index].Width + CardGap));

                _flow.Page.FillRectangle(left, _flow.Top, cards[index].Width, height, ReportPdfTheme.CardFill);
                DrawCard(cards[index], left + CardHorizontalPadding, _flow.Top + CardVerticalPadding);
            }

            _flow.Advance(height + CardGap);
        }

        _flow.AddSpace(SectionSpacing - CardGap);
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

        return new Card(parts, cardWidth, height);
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

    private List<Bar> CreateBars(IList<ReportBar> bars)
    {
        return (bars ?? [])
            .Where(bar => bar is not null)
            .Select(CreateBar)
            .ToList();
    }

    private Bar CreateBar(ReportBar bar)
    {
        // The value ends the first line of the label, which wraps before it.
        var value = _flow.Bold.Prepare(bar.Value).Replace('\n', ' ');
        var width = Math.Max(_flow.Width / 4, _flow.Width - _flow.Bold.Measure(value, DescriptionSize) - 8);
        var lines = ReportPdfText.Wrap(_flow.Regular.Prepare(bar.Label), _flow.Regular, DescriptionSize, width);
        var ratio = double.IsFinite(bar.Ratio) ? Math.Clamp(bar.Ratio, 0, 1) : 0;
        var height = (Math.Max(1, lines.Count) * _flow.Regular.LineHeight * DescriptionSize) + 2 + BarHeight;

        return new Bar(value, lines, ratio, height);
    }

    private void WriteBars(List<Bar> bars)
    {
        if (bars.Count == 0)
        {
            return;
        }

        foreach (var bar in bars)
        {
            // The label and value above, and below them the filled and empty parts of the bar, whose widths follow the
            // ratio, like the progress bars on screen.
            _flow.EnsureSpace(bar.Height);

            var top = _flow.Top;

            for (var index = 0; index < bar.Lines.Count; index++)
            {
                _flow.Page.DrawText(bar.Lines[index], _flow.Regular, DescriptionSize, _flow.Left, top + (index * _flow.Regular.LineHeight * DescriptionSize), ReportPdfTheme.Text);
            }

            _flow.Page.DrawText(bar.Value, _flow.Bold, DescriptionSize, _flow.Left + _flow.Width - _flow.Bold.Measure(bar.Value, DescriptionSize), top, ReportPdfTheme.Text);

            var trackTop = top + bar.Height - BarHeight;

            _flow.Page.FillRectangle(_flow.Left, trackTop, _flow.Width, BarHeight, ReportPdfTheme.BarTrack);
            _flow.Page.FillRectangle(_flow.Left, trackTop, _flow.Width * bar.Ratio, BarHeight, ReportPdfTheme.GetPaletteColor(0));
            _flow.Advance(bar.Height + 6);
        }

        _flow.AddSpace(SectionSpacing);
    }

    // The title band of the container when the section is the container's first, the title of the section, and what
    // filters it and its description, wrapped, with their height on the page.
    private sealed record Head(List<string> Band, List<string> Title, List<string> Description, double Height);

    private sealed record CardPart(List<string> Lines, ReportPdfFont Font, double Size, ReportPdfColor Color, double SpaceBefore);

    private sealed record Card(List<CardPart> Parts, double Width, double Height);

    private sealed record Bar(string Value, List<string> Lines, double Ratio, double Height);
}
