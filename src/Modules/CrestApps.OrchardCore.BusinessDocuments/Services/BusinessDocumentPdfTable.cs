using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Pdf;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// Writes the line table of a business document: its header row, filled with the accent color, on every page it spans,
/// item rows whose cells wrap, section, subtotal and note rows. A row that does not fit on the page moves to the next one
/// whole; only a row taller than a page is split, between its lines.
/// </summary>
internal sealed class BusinessDocumentPdfTable
{
    private const double HorizontalPadding = 5;
    private const double VerticalPadding = 4;

    // A column sized to its content takes at most this share of the table, so one long value cannot squeeze the others.
    private const double MaximumAutomaticShare = 0.45;

    // The share of the table left to the columns with a relative width, however wide the others are.
    private const double MinimumWeightedShare = 0.25;

    private readonly PdfFlow _flow;
    private readonly List<BusinessDocumentColumn> _columns;
    private readonly List<BusinessDocumentRow> _rows;
    private readonly PdfColor? _accent;
    private readonly bool _darkHeaderText;
    private readonly double[] _widths;
    private readonly double[] _lefts;
    private readonly Row _header;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentPdfTable"/> class.
    /// </summary>
    /// <param name="flow">The flow the table is written in.</param>
    /// <param name="columns">The columns.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="accentColor">The accent color, written <c>#RRGGBB</c>, or <see langword="null"/>.</param>
    public BusinessDocumentPdfTable(
        PdfFlow flow,
        List<BusinessDocumentColumn> columns,
        List<BusinessDocumentRow> rows,
        string accentColor)
    {
        _flow = flow;
        _columns = columns;
        _rows = rows;
        _accent = accentColor is not null && PdfColor.TryParseHex(accentColor, out var accent) ? accent : null;
        _darkHeaderText = BusinessDocumentStyle.PrefersDarkText(accentColor);
        _widths = GetColumnWidths();
        _lefts = new double[_widths.Length];

        for (var index = 1; index < _widths.Length; index++)
        {
            _lefts[index] = _lefts[index - 1] + _widths[index - 1];
        }

        _header = CreateHeader();
    }

    /// <summary>
    /// Gets the height of the start of the table, which must fit on the page it starts on: its header and first row.
    /// </summary>
    public double LeadHeight
        => _header.Height + (_rows.Count > 0 ? Math.Min(CreateRow(_rows[0]).Height, _flow.PageContentHeight / 3) : 0);

    /// <summary>
    /// Writes the table, from the next page when its start does not fit on this one.
    /// </summary>
    public void Write()
    {
        _flow.EnsureSpace(LeadHeight);
        Draw(_header, 0, _header.ContentHeight);

        foreach (var source in _rows)
        {
            var row = CreateRow(source);

            if (row.Height > _flow.Remaining)
            {
                // A row that fits on a page under the header moves there whole.
                if (row.Height <= _flow.PageContentHeight - _header.Height)
                {
                    NewPage();
                }
                else
                {
                    WriteSplit(row);

                    continue;
                }
            }

            Draw(row, 0, row.ContentHeight);
        }
    }

    private void WriteSplit(Row row)
    {
        var breaks = row.Cells
            .SelectMany(cell => cell.Block.GetBreaks())
            .Distinct()
            .Order()
            .ToList();
        var from = 0d;

        while (from < row.ContentHeight - 0.01)
        {
            var room = _flow.Remaining - (2 * VerticalPadding);
            var to = breaks.Where(value => value > from + 0.01 && value - from <= room).DefaultIfEmpty(from).Max();

            if (to <= from + 0.01)
            {
                // Not even one line fits: on a page that has other rows, the row continues on the next page; on a page of
                // its own, its next line is drawn anyway.
                if (_flow.Remaining < _flow.PageContentHeight - _header.Height - 0.5)
                {
                    NewPage();

                    continue;
                }

                to = breaks.FirstOrDefault(value => value > from + 0.01, row.ContentHeight);
            }

            Draw(row, from, to);
            from = to;

            if (from < row.ContentHeight - 0.01)
            {
                NewPage();
            }
        }
    }

    private void NewPage()
    {
        _flow.NewPage();
        Draw(_header, 0, _header.ContentHeight);
    }

    private void Draw(Row row, double from, double to)
    {
        var page = _flow.Page;
        var top = _flow.Top;
        var height = (to - from) + (2 * VerticalPadding);
        var width = _widths.Sum();

        if (row.Fill is { } fill)
        {
            page.FillRectangle(_flow.Left, top, width, height, fill);
        }

        if (row.TopRule && from <= 0)
        {
            page.DrawLine(_flow.Left, top, _flow.Left + width, top, BusinessDocumentPdfTheme.Rule, 0.75);
        }

        foreach (var cell in row.Cells)
        {
            cell.Block.Draw(page, _flow.Left + cell.Left + HorizontalPadding, top + VerticalPadding, from, to);
        }

        page.DrawLine(_flow.Left, top + height, _flow.Left + width, top + height, BusinessDocumentPdfTheme.Border, 0.5);
        _flow.Advance(height);
    }

    private Row CreateHeader()
    {
        var textColor = _accent is null || _darkHeaderText ? BusinessDocumentPdfTheme.Text : BusinessDocumentPdfTheme.White;
        var cells = new List<Cell>();

        for (var index = 0; index < _columns.Count; index++)
        {
            var block = new BusinessDocumentPdfBlock(_widths[index] - (2 * HorizontalPadding))
                .AddText(_columns[index].Title, _flow.Bold, BusinessDocumentPdfTheme.SmallSize, textColor, _columns[index].Alignment);

            cells.Add(new Cell(_lefts[index], block));
        }

        return new Row(cells, _accent ?? BusinessDocumentPdfTheme.HeaderFill, topRule: false);
    }

    private Row CreateRow(BusinessDocumentRow row)
    {
        var width = _widths.Sum() - (2 * HorizontalPadding);

        switch (row.Kind)
        {
            case BusinessDocumentRowKind.Section:
                var section = new BusinessDocumentPdfBlock(width)
                    .AddText(GetSpanningText(row), _flow.Bold, BusinessDocumentPdfTheme.BaseSize, BusinessDocumentPdfTheme.Text)
                    .AddText(row.Detail, _flow.Regular, BusinessDocumentPdfTheme.SmallSize, BusinessDocumentPdfTheme.Muted);

                return new Row([new Cell(0, section)], _accent?.Lighten(0.88) ?? BusinessDocumentPdfTheme.SectionFill, topRule: false);
            case BusinessDocumentRowKind.Note:
                var note = new BusinessDocumentPdfBlock(width)
                    .AddText(GetSpanningText(row), _flow.Regular, BusinessDocumentPdfTheme.SmallSize, BusinessDocumentPdfTheme.Muted)
                    .AddText(row.Detail, _flow.Regular, BusinessDocumentPdfTheme.SmallSize, BusinessDocumentPdfTheme.Muted);

                return new Row([new Cell(0, note)], null, topRule: false);
        }

        var subtotal = row.Kind == BusinessDocumentRowKind.Subtotal;
        var font = subtotal ? _flow.Bold : _flow.Regular;
        var cells = new List<Cell>();

        for (var index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            var block = new BusinessDocumentPdfBlock(_widths[index] - (2 * HorizontalPadding))
                .AddText(GetCell(row, column), font, BusinessDocumentPdfTheme.BaseSize, BusinessDocumentPdfTheme.Text, column.Alignment);

            if (index == 0)
            {
                block.AddText(row.Detail, _flow.Regular, BusinessDocumentPdfTheme.SmallSize, BusinessDocumentPdfTheme.Muted, column.Alignment);
            }

            cells.Add(new Cell(_lefts[index], block));
        }

        return new Row(cells, subtotal ? BusinessDocumentPdfTheme.SectionFill : null, topRule: subtotal);
    }

    private double[] GetColumnWidths()
    {
        var total = _flow.Width;
        var count = _columns.Count;
        var natural = new double[count];
        var minimum = new double[count];

        for (var index = 0; index < count; index++)
        {
            var column = _columns[index];
            var values = _rows
                .Where(row => row.Kind is BusinessDocumentRowKind.Item or BusinessDocumentRowKind.Subtotal)
                .Select(row => GetCell(row, column))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => (Font: _flow.Regular, Size: BusinessDocumentPdfTheme.BaseSize, Value: value))
                .Prepend((Font: _flow.Bold, Size: BusinessDocumentPdfTheme.SmallSize, Value: column.Title ?? string.Empty))
                .ToList();

            natural[index] = values.Max(value => Measure(value.Font, value.Size, value.Value, longestWord: false)) + (2 * HorizontalPadding);
            minimum[index] = Math.Min(values.Max(value => Measure(value.Font, value.Size, value.Value, longestWord: true)), total * MaximumAutomaticShare) + (2 * HorizontalPadding);
        }

        var widths = new double[count];
        var weights = _columns.Sum(column => column.Width > 0 ? column.Width : 0);

        if (weights > 0)
        {
            var automatic = 0d;

            for (var index = 0; index < count; index++)
            {
                if (_columns[index].Width <= 0)
                {
                    widths[index] = Math.Min(natural[index], total * MaximumAutomaticShare);
                    automatic += widths[index];
                }
            }

            var remaining = total - automatic;

            if (remaining < total * MinimumWeightedShare)
            {
                var scale = automatic > 0 ? (total * (1 - MinimumWeightedShare)) / automatic : 0;

                for (var index = 0; index < count; index++)
                {
                    if (_columns[index].Width <= 0)
                    {
                        widths[index] *= scale;
                    }
                }

                remaining = total * MinimumWeightedShare;
            }

            for (var index = 0; index < count; index++)
            {
                if (_columns[index].Width > 0)
                {
                    widths[index] = remaining * _columns[index].Width / weights;
                }
            }

            return widths;
        }

        var naturalTotal = natural.Sum();

        if (naturalTotal <= total)
        {
            // The first column, usually the description, takes the room the others do not need.
            natural.CopyTo(widths, 0);
            widths[0] += total - naturalTotal;

            return widths;
        }

        var minimumTotal = minimum.Sum();

        if (minimumTotal >= total)
        {
            for (var index = 0; index < count; index++)
            {
                widths[index] = minimum[index] * total / minimumTotal;
            }

            return widths;
        }

        // Every column keeps its longest word whole, and the rest of the table is shared by what each needs beyond it.
        var excess = natural.Select((value, index) => Math.Max(0, value - minimum[index])).ToArray();
        var excessTotal = excess.Sum();

        for (var index = 0; index < count; index++)
        {
            widths[index] = minimum[index] + (excessTotal > 0 ? (total - minimumTotal) * excess[index] / excessTotal : 0);
        }

        return widths;
    }

    private static double Measure(PdfFontFace font, double size, string value, bool longestWord)
    {
        var text = font.Prepare(value);
        var parts = longestWord
            ? text.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries)
            : text.Split('\n');

        return parts.Length == 0 ? 0 : parts.Max(part => font.Measure(part, size));
    }

    private string GetSpanningText(BusinessDocumentRow row)
        => GetCell(row, _columns[0]) ?? row.Cells?.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string GetCell(BusinessDocumentRow row, BusinessDocumentColumn column)
        => column.Name is not null && row.Cells is not null && row.Cells.TryGetValue(column.Name, out var value) ? value : null;

    private sealed record Cell(double Left, BusinessDocumentPdfBlock Block);

    private sealed class Row
    {
        public Row(
            List<Cell> cells,
            PdfColor? fill,
            bool topRule)
        {
            Cells = cells;
            Fill = fill;
            TopRule = topRule;
            ContentHeight = Math.Max(cells.Count == 0 ? 0 : cells.Max(cell => cell.Block.Height), 1);
        }

        public List<Cell> Cells { get; }

        public PdfColor? Fill { get; }

        public bool TopRule { get; }

        public double ContentHeight { get; }

        public double Height => ContentHeight + (2 * VerticalPadding);
    }
}
