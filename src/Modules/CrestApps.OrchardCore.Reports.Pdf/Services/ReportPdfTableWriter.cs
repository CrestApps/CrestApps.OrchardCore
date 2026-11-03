using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Writes a table section: its header row on every page it spans, the shading of its subtotal and total rows, and the
/// colors and weight of its cell styles. A cell wraps between its words, and a word too long for its column is broken.
/// A row that does not fit on the page moves to the next one whole, unless it would not fit there either, under the
/// header or, for the first row, under the head of the section: it is then split where the page ends.
/// </summary>
internal sealed class ReportPdfTableWriter
{
    private const double VerticalPadding = 3;
    private const double HorizontalPadding = ReportPdfTableLayout.CellPadding / 2;

    private readonly ReportPdfFlow _flow;
    private readonly ReportSection _section;
    private readonly ReportPdfTableLayout _layout;
    private readonly double[] _widths;
    private readonly double[] _lefts;
    private readonly double _lineHeight;
    private readonly Row _header;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportPdfTableWriter"/> class.
    /// </summary>
    /// <param name="flow">The flow the table is written in.</param>
    /// <param name="section">The table section.</param>
    /// <param name="layout">The measured columns of the table.</param>
    /// <param name="space">The height a page has under the head of the section, in points.</param>
    public ReportPdfTableWriter(
        ReportPdfFlow flow,
        ReportSection section,
        ReportPdfTableLayout layout,
        double space)
    {
        _flow = flow;
        _section = section;
        _layout = layout;
        _widths = layout.GetColumnWidths(flow.Width);
        _lefts = new double[_widths.Length];

        for (var index = 1; index < _widths.Length; index++)
        {
            _lefts[index] = _lefts[index - 1] + _widths[index - 1];
        }

        _lineHeight = Math.Max(flow.Regular.LineHeight, flow.Bold.LineHeight) * layout.FontSize;
        _header = CreateHeader();
        LeadHeight = GetLeadHeight(space);
    }

    /// <summary>
    /// Gets the height of the start of the table, which the head of the section stays with: its header and first row,
    /// or only the first line of that row when the row does not fit in the space under the head.
    /// </summary>
    public double LeadHeight { get; }

    /// <summary>
    /// Writes the table, from the next page when its start does not fit on this one.
    /// </summary>
    public void Write()
    {
        _flow.EnsureSpace(LeadHeight);
        WriteHeader();

        var rowsOnPage = 0;

        foreach (var reportRow in _section.Rows ?? [])
        {
            if (reportRow is null)
            {
                continue;
            }

            var row = CreateRow(reportRow);
            var lineCount = Math.Max(1, row.LineCount);
            var line = 0;

            while (line < lineCount)
            {
                var left = lineCount - line;

                if ((left * _lineHeight) + (2 * VerticalPadding) <= _flow.Remaining)
                {
                    DrawRow(row, line, left, top: line == 0);
                    rowsOnPage++;

                    break;
                }

                // A row that is not the first on its page moves to the next one; the first is split where the page ends.
                var fitting = (int)Math.Floor((_flow.Remaining - (2 * VerticalPadding)) / _lineHeight);

                if (rowsOnPage == 0)
                {
                    fitting = Math.Max(1, fitting);
                    DrawRow(row, line, fitting, top: line == 0);
                    line += fitting;
                }

                _flow.NewPage();
                WriteHeader();
                rowsOnPage = 0;
            }
        }
    }

    private double GetLeadHeight(double space)
    {
        var header = GetHeight(_header);
        var first = (_section.Rows ?? []).FirstOrDefault(row => row is not null);

        if (first is null)
        {
            return header;
        }

        var whole = header + GetHeight(CreateRow(first));

        // A first row that does not fit under the head even at the top of a page is split where the page ends, so only
        // its first line stays with the head.
        return whole <= space
            ? whole
            : header + _lineHeight + (2 * VerticalPadding);
    }

    private void WriteHeader()
    {
        if (_header is not null)
        {
            DrawRow(_header, 0, Math.Max(1, _header.LineCount), top: false);
        }
    }

    private Row CreateHeader()
    {
        var columns = _section.Columns ?? [];

        if (columns.Count == 0)
        {
            return null;
        }

        var cells = new Cell[_layout.ColumnCount];

        for (var index = 0; index < cells.Length; index++)
        {
            var column = index < columns.Count ? columns[index] : null;

            cells[index] = CreateCell(index, column?.Label, bold: true, column?.HeaderStyle);
        }

        return new Row(cells, ReportPdfTheme.HeaderFill, ReportPdfTheme.Muted, 0.75, topRule: false);
    }

    private Row CreateRow(ReportRow row)
    {
        var values = row.Cells ?? [];
        var total = row.Kind != ReportRowKind.Detail;
        var cells = new Cell[_layout.ColumnCount];

        for (var index = 0; index < cells.Length; index++)
        {
            cells[index] = CreateCell(index, index < values.Count ? values[index] : null, total, row.GetCellStyle(index));
        }

        return total
            ? new Row(cells, row.Kind == ReportRowKind.Subtotal ? ReportPdfTheme.SubtotalFill : ReportPdfTheme.GrandTotalFill, ReportPdfTheme.Border, 0.5, topRule: true)
            : new Row(cells, null, ReportPdfTheme.Border, 0.5, topRule: false);
    }

    private Cell CreateCell(int index, string value, bool bold, ReportStyle style)
    {
        var hasStyle = style is not null && !style.IsEmpty;
        var font = bold || (hasStyle && style.Bold)
            ? _flow.Bold
            : _flow.Regular;
        var color = hasStyle && ReportPdfColor.TryParse(style.Color, out var textColor) ? textColor : ReportPdfTheme.Text;
        ReportPdfColor? fill = hasStyle && ReportPdfColor.TryParse(style.BackgroundColor, out var background) ? background : null;
        var lines = ReportPdfText.Wrap(font.Prepare(value), font, _layout.FontSize, _widths[index] - ReportPdfTableLayout.CellPadding);

        return new Cell(lines, font, color, fill, GetAlignment(index));
    }

    private ReportColumnAlign GetAlignment(int index)
    {
        var columns = _section.Columns ?? [];

        return index < columns.Count && columns[index] is { } column ? column.Align : ReportColumnAlign.Start;
    }

    private double GetHeight(Row row)
    {
        return row is null ? 0 : (Math.Max(1, row.LineCount) * _lineHeight) + (2 * VerticalPadding);
    }

    private void DrawRow(Row row, int firstLine, int lineCount, bool top)
    {
        var page = _flow.Page;
        var y = _flow.Top;
        var height = (lineCount * _lineHeight) + (2 * VerticalPadding);
        var tableWidth = _widths.Sum();

        if (row.Fill is { } rowFill)
        {
            page.FillRectangle(_flow.Left, y, tableWidth, height, rowFill);
        }

        for (var index = 0; index < row.Cells.Length; index++)
        {
            var cell = row.Cells[index];
            var left = _flow.Left + _lefts[index];

            if (cell.Fill is { } cellFill)
            {
                page.FillRectangle(left, y, _widths[index], height, cellFill);
            }

            for (var line = firstLine; line < Math.Min(cell.Lines.Count, firstLine + lineCount); line++)
            {
                var text = cell.Lines[line];
                var textWidth = cell.Font.Measure(text, _layout.FontSize);
                var x = cell.Align switch
                {
                    ReportColumnAlign.End => left + _widths[index] - HorizontalPadding - textWidth,
                    ReportColumnAlign.Center => left + ((_widths[index] - textWidth) / 2),
                    _ => left + HorizontalPadding,
                };

                page.DrawText(text, cell.Font, _layout.FontSize, x, y + VerticalPadding + ((line - firstLine) * _lineHeight), cell.Color);
            }
        }

        if (row.TopRule && top)
        {
            page.DrawLine(_flow.Left, y, _flow.Left + tableWidth, y, ReportPdfTheme.Muted, 0.75);
        }

        page.DrawLine(_flow.Left, y + height, _flow.Left + tableWidth, y + height, row.RuleColor, row.RuleWidth);
        _flow.Advance(height);
    }

    private sealed record Cell(List<string> Lines, ReportPdfFont Font, ReportPdfColor Color, ReportPdfColor? Fill, ReportColumnAlign Align);

    private sealed class Row
    {
        public Row(
            Cell[] cells,
            ReportPdfColor? fill,
            ReportPdfColor ruleColor,
            double ruleWidth,
            bool topRule)
        {
            Cells = cells;
            Fill = fill;
            RuleColor = ruleColor;
            RuleWidth = ruleWidth;
            TopRule = topRule;
            LineCount = cells.Max(cell => cell.Lines.Count);
        }

        public Cell[] Cells { get; }

        public ReportPdfColor? Fill { get; }

        public ReportPdfColor RuleColor { get; }

        public double RuleWidth { get; }

        public bool TopRule { get; }

        public int LineCount { get; }
    }
}
