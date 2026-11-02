using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer;

public sealed partial class ReportDesignDocumentBuilder
{
    private const char KeySeparator = '\u001E';

    // The cross-tab a table lays out from the shelves: the dimensions of the Rows shelf down the side, those of the
    // Columns shelf across the top, and the measures in the cells. Null when the table shows its columns side by side:
    // when it lists its own columns, when nothing is aggregated, or when no dimension and no measure is on the Rows
    // shelf. Measures are listed down the side, one row each, when every measure is on the Rows shelf.
    private static CrosstabLayout GetShelfLayout(ReportVisualDefinition visual, IReadOnlyList<ReportResultColumn> columns)
    {
        if (visual.Type != ReportVisualType.Table || visual.ColumnIds.Count > 0)
        {
            return null;
        }

        var visible = columns.Where(column => !column.Hidden).ToList();
        var measures = visible.Where(column => column.IsMeasure).ToList();

        if (measures.Count == 0)
        {
            return null;
        }

        var rows = visible.Where(column => !column.IsMeasure && column.Shelf == ReportColumnShelf.Rows).Select(column => column.Id).ToArray();
        var across = visible.Where(column => !column.IsMeasure && column.Shelf == ReportColumnShelf.Columns).Select(column => column.Id).ToArray();
        var measuresOnRows = measures.All(column => column.Shelf == ReportColumnShelf.Rows);

        // Without dimensions across the top, a table of the Rows dimensions and the measures is the usual table.
        if (!measuresOnRows && (rows.Length == 0 || across.Length == 0))
        {
            return null;
        }

        return new CrosstabLayout(rows, across, [.. measures.Select(column => column.Id)], measuresOnRows, visual.ShowTotals);
    }

    // The cross-tab of a pivot table: its row columns down the side, the values of its series column across the top,
    // and its value columns in the cells.
    private static CrosstabLayout GetPivotLayout(ReportVisualDefinition visual, IReadOnlyList<ReportResultColumn> columns)
    {
        bool Exists(string id) => columns.Any(column => column.Id == id);

        var rows = visual.ColumnIds.Where(Exists).Distinct(StringComparer.Ordinal).ToArray();
        var values = visual.ValueColumnIds.Where(Exists).Distinct(StringComparer.Ordinal).ToArray();

        if (rows.Length == 0 || values.Length == 0 || !Exists(visual.SeriesColumnId) || rows.Contains(visual.SeriesColumnId, StringComparer.Ordinal))
        {
            return null;
        }

        return new CrosstabLayout(rows, [visual.SeriesColumnId], values, MeasuresOnRows: false, visual.ShowTotals);
    }

    private ReportSection BuildCrosstab(ReportVisualDefinition visual, ReportQueryResult result, CrosstabLayout layout)
    {
        var rowIndexes = layout.RowIds.Select(result.IndexOf).ToArray();
        var acrossIndexes = layout.AcrossIds.Select(result.IndexOf).ToArray();
        var valueIndexes = layout.ValueIds.Select(result.IndexOf).ToArray();
        var measures = valueIndexes.Select(index => result.Columns[index]).ToArray();
        var hasAcross = acrossIndexes.Length > 0;
        var showTotals = layout.ShowTotals && hasAcross;
        var perCombination = layout.MeasuresOnRows ? 1 : measures.Length;

        // The combinations of the values across the top, ordered by each dimension in turn, with their column totals.
        var combinations = hasAcross
            ? result.Regroup(layout.AcrossIds)
                .Select(group => group.Values)
                .Order(Comparer<object[]>.Create((left, right) => CompareLevels(left, right, acrossIndexes)))
                .ToList()
            : [null];
        var cells = hasAcross
            ? result.Regroup(layout.Groupings[0]).ToDictionary(
                group => RowKey(group.Values, rowIndexes) + KeySeparator + RowKey(group.Values, acrossIndexes),
                group => group.Values,
                StringComparer.Ordinal)
            : null;
        var rowGroups = rowIndexes.Length == 0
            ? result.Regroup([]).Select(group => group.Values).ToList()
            : KeepTogether(result.Regroup(layout.RowIds).Select(group => group.Values).ToList(), rowIndexes);

        var columns = new List<ReportColumn>();

        foreach (var index in rowIndexes)
        {
            columns.Add(new ReportColumn(result.Columns[index].Label, Align(result.Columns[index]), ReportColumnColorizer.HeaderStyleOf(result.Columns[index])));
        }

        if (layout.MeasuresOnRows)
        {
            columns.Add(new ReportColumn(string.Empty));
        }

        foreach (var combination in combinations)
        {
            if (layout.MeasuresOnRows)
            {
                columns.Add(new ReportColumn(hasAcross ? _formatter.Format(combination[acrossIndexes[^1]], result.Columns[acrossIndexes[^1]]) : S["Value"], ReportColumnAlign.End));

                continue;
            }

            foreach (var measure in measures)
            {
                var label = !hasAcross || measures.Length > 1
                    ? measure.Label
                    : _formatter.Format(combination[acrossIndexes[^1]], result.Columns[acrossIndexes[^1]]);

                columns.Add(new ReportColumn(label, ReportColumnAlign.End, ReportColumnColorizer.HeaderStyleOf(measure)));
            }
        }

        if (showTotals)
        {
            for (var position = 0; position < perCombination; position++)
            {
                columns.Add(new ReportColumn(
                    perCombination > 1 ? measures[position].ValueLabel ?? measures[position].Label : S["Total"],
                    ReportColumnAlign.End,
                    layout.MeasuresOnRows ? null : ReportColumnColorizer.HeaderStyleOf(measures[position])));
            }
        }

        // Each row: its header cells, then a value and its measure for each cell; totals are left uncolored.
        var rows = new List<(List<string> Headers, List<(object Value, int Measure, bool IsTotal)> Values, ReportRowKind Kind)>();
        var previous = default(object[]);

        object[] CellValues(object[] rowValues, object[] combination)
        {
            return hasAcross
                ? cells.GetValueOrDefault(RowKey(rowValues, rowIndexes) + KeySeparator + RowKey(combination, acrossIndexes))
                : rowValues;
        }

        void AddRow(List<string> headers, object[] rowValues, Func<object[], object[]> cellValues, int measure, ReportRowKind kind)
        {
            var values = new List<(object, int, bool)>();

            for (var position = 0; position < combinations.Count; position++)
            {
                var source = cellValues(combinations[position]);

                if (measure >= 0)
                {
                    values.Add((source?[valueIndexes[measure]], measure, kind != ReportRowKind.Detail));

                    continue;
                }

                for (var current = 0; current < measures.Length; current++)
                {
                    values.Add((source?[valueIndexes[current]], current, kind != ReportRowKind.Detail));
                }
            }

            if (showTotals)
            {
                for (var current = 0; current < measures.Length; current++)
                {
                    if (measure < 0 || measure == current)
                    {
                        values.Add((rowValues[valueIndexes[current]], current, true));
                    }
                }
            }

            rows.Add((headers, values, kind));
        }

        foreach (var rowValues in rowGroups)
        {
            var headers = RowHeaders(rowValues, previous, rowIndexes, result);

            previous = rowValues;

            if (!layout.MeasuresOnRows)
            {
                AddRow(headers, rowValues, combination => CellValues(rowValues, combination), -1, ReportRowKind.Detail);

                continue;
            }

            for (var measure = 0; measure < measures.Length; measure++)
            {
                var measureHeaders = measure == 0 ? [.. headers] : headers.Select(_ => string.Empty).ToList();

                measureHeaders.Add(measures[measure].Label);
                AddRow(measureHeaders, rowValues, combination => CellValues(rowValues, combination), measure, ReportRowKind.Detail);
            }
        }

        if (layout.ShowTotals && rowIndexes.Length > 0)
        {
            var grand = result.GetTotals();
            var columnTotals = hasAcross
                ? result.Regroup(layout.AcrossIds).ToDictionary(group => RowKey(group.Values, acrossIndexes), group => group.Values, StringComparer.Ordinal)
                : null;

            object[] TotalValues(object[] combination)
            {
                return hasAcross ? columnTotals.GetValueOrDefault(RowKey(combination, acrossIndexes)) : grand;
            }

            for (var measure = layout.MeasuresOnRows ? 0 : -1; measure < (layout.MeasuresOnRows ? measures.Length : 0); measure++)
            {
                var headers = rowIndexes.Select(_ => string.Empty).ToList();

                if (measure <= 0)
                {
                    headers[0] = S["Total"];
                }

                // Totals show the plain values, under the plain name of a measure shown as a calculation.
                if (layout.MeasuresOnRows)
                {
                    headers.Add(measures[measure].ValueLabel ?? measures[measure].Label);
                }

                AddRow(headers, grand, TotalValues, measure, ReportRowKind.GrandTotal);
            }
        }

        var calculated = valueIndexes.Select(index => IsCalculated(result, index)).ToArray();

        for (var measure = 0; measure < measures.Length; measure++)
        {
            if (calculated[measure])
            {
                Calculate(rows.Where(row => row.Kind == ReportRowKind.Detail).Select(row => row.Values).ToList(), measure, measures[measure]);
            }
        }

        var colorizers = measures
            .Select((measure, position) => ReportColumnColorizer.Create(ShownColumn(measure, calculated[position]), rows
                .SelectMany(row => row.Values)
                .Where(cell => cell.Measure == position && !cell.IsTotal)
                .Select(cell => cell.Value)))
            .ToArray();

        var reportRows = rows.Select(row =>
        {
            var reportRow = new ReportRow(
                [.. row.Headers, .. row.Values.Select(cell => FormatShown(cell.Value, measures[cell.Measure], calculated[cell.Measure] && !cell.IsTotal))],
                row.Kind);

            for (var position = 0; position < row.Values.Count; position++)
            {
                var (value, measure, isTotal) = row.Values[position];
                var style = isTotal ? null : colorizers[measure]?.StyleOf(value);

                if (style is not null)
                {
                    reportRow.WithCellStyle(row.Headers.Count + position, style);
                }
            }

            return reportRow;
        }).ToList();

        var section = ReportSection.ForTable(visual.Title, columns, reportRows);

        if (hasAcross)
        {
            section.HeaderRows = HeaderRows(result, layout, acrossIndexes, combinations, perCombination, showTotals);
        }

        return section;
    }

    // Shows the cells of a measure of a cross-tab as its quick table calculation: the cells of each detail row, by the
    // combinations across the top, form the grid it is computed over. Row totals and totals rows keep the plain totals.
    private static void Calculate(List<List<(object Value, int Measure, bool IsTotal)>> rows, int measure, ReportResultColumn column)
    {
        var positions = rows
            .Select(values => Enumerable.Range(0, values.Count).Where(position => values[position].Measure == measure && !values[position].IsTotal).ToArray())
            .ToList();
        var grid = rows
            .Select((values, row) => positions[row].Select(position => values[position].Value).ToArray())
            .Where(cells => cells.Length > 0)
            .ToArray();
        var withCells = Enumerable.Range(0, rows.Count).Where(row => positions[row].Length > 0).ToArray();

        ReportTableCalculations.Apply(grid, column.ShowAs, column.ShowAsDirection);

        for (var row = 0; row < withCells.Length; row++)
        {
            var values = rows[withCells[row]];
            var cells = positions[withCells[row]];

            for (var cell = 0; cell < cells.Length; cell++)
            {
                values[cells[cell]] = (grid[row][cell], measure, false);
            }
        }
    }

    // The header rows above a cross-tab's column headers: the names of the dimensions across the top, then the values
    // of each outer dimension over the columns they group. The innermost dimension heads the columns themselves, unless
    // the measures do, in which case its values get a row too.
    private List<ReportHeaderRow> HeaderRows(
        ReportQueryResult result,
        CrosstabLayout layout,
        int[] acrossIndexes,
        List<object[]> combinations,
        int perCombination,
        bool showTotals)
    {
        var lead = layout.RowIds.Count + (layout.MeasuresOnRows ? 1 : 0);
        var totals = showTotals ? perCombination : 0;
        var headerRows = new List<ReportHeaderRow>();

        List<ReportHeaderCell> Start()
        {
            return lead > 0 ? [new ReportHeaderCell(string.Empty, lead)] : [];
        }

        var caption = Start();

        caption.Add(new ReportHeaderCell(string.Join(" / ", acrossIndexes.Select(index => result.Columns[index].Label)), combinations.Count * perCombination));

        if (totals > 0)
        {
            caption.Add(new ReportHeaderCell(string.Empty, totals));
        }

        headerRows.Add(new ReportHeaderRow(caption));

        var levels = perCombination > 1 ? acrossIndexes.Length : acrossIndexes.Length - 1;

        for (var level = 0; level < levels; level++)
        {
            var row = Start();
            var span = 0;

            for (var position = 0; position < combinations.Count; position++)
            {
                span += perCombination;

                var last = position == combinations.Count - 1;

                if (last || CompareLevels(combinations[position], combinations[position + 1], acrossIndexes[..(level + 1)]) != 0)
                {
                    row.Add(new ReportHeaderCell(_formatter.Format(combinations[position][acrossIndexes[level]], result.Columns[acrossIndexes[level]]), span));
                    span = 0;
                }
            }

            if (totals > 0)
            {
                row.Add(new ReportHeaderCell(level == 0 && perCombination > 1 ? S["Total"] : string.Empty, totals));
            }

            headerRows.Add(new ReportHeaderRow(row));
        }

        return headerRows;
    }

    // The header cells of a row of a cross-tab: the values of its row dimensions, leaving out the outer values it
    // shares with the row above, so each group of rows is headed once.
    private List<string> RowHeaders(object[] values, object[] previous, int[] rowIndexes, ReportQueryResult result)
    {
        var headers = new List<string>(rowIndexes.Length);
        var same = previous is not null;

        for (var level = 0; level < rowIndexes.Length; level++)
        {
            same = same && level < rowIndexes.Length - 1 && DataValues.AreEqual(values[rowIndexes[level]], previous[rowIndexes[level]]);
            headers.Add(same ? string.Empty : _formatter.Format(values[rowIndexes[level]], result.Columns[rowIndexes[level]]));
        }

        return headers;
    }

    // Keeps rows that share outer values together, in the order each group first appears, without reordering the rows
    // within a group, so the sort of the result still orders each group.
    private static List<object[]> KeepTogether(List<object[]> rows, int[] indexes)
    {
        for (var level = indexes.Length - 2; level >= 0; level--)
        {
            var levelIndexes = indexes[..(level + 1)];
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var position = 0; position < rows.Count; position++)
            {
                positions.TryAdd(RowKey(rows[position], levelIndexes), position);
            }

            rows = rows
                .Select((values, position) => (values, position))
                .OrderBy(item => positions[RowKey(item.values, levelIndexes)])
                .ThenBy(item => item.position)
                .Select(item => item.values)
                .ToList();
        }

        return rows;
    }

    private static int CompareLevels(object[] left, object[] right, int[] indexes)
    {
        foreach (var index in indexes)
        {
            var compared = DataValues.Compare(left[index], right[index]);

            if (compared != 0)
            {
                return compared;
            }
        }

        return 0;
    }

    // The dimensions and measures of a cross-tab, and the groupings it reads from the result, in the order its build
    // asks for them: the cells, then the rows and the columns with their totals, then the grand total.
    private sealed record CrosstabLayout(IReadOnlyList<string> RowIds, IReadOnlyList<string> AcrossIds, IReadOnlyList<string> ValueIds, bool MeasuresOnRows, bool ShowTotals)
    {
        public List<IReadOnlyList<string>> Groupings
        {
            get
            {
                var groupings = new List<IReadOnlyList<string>> { (IReadOnlyList<string>)[.. RowIds, .. AcrossIds] };

                if (AcrossIds.Count > 0)
                {
                    if (RowIds.Count > 0)
                    {
                        groupings.Add(RowIds);
                    }

                    groupings.Add(AcrossIds);
                }

                groupings.Add([]);

                return groupings;
            }
        }
    }
}
