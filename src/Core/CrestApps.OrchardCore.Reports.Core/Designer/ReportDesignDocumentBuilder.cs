using System.Globalization;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Turns a designed query result and its visuals into a <see cref="ReportDocument"/>, so designed reports render and
/// export through the same pipeline as every other report.
/// </summary>
public sealed partial class ReportDesignDocumentBuilder
{
    private readonly ReportValueFormatter _formatter;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignDocumentBuilder"/> class.
    /// </summary>
    /// <param name="formatter">The value formatter.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignDocumentBuilder(
        ReportValueFormatter formatter,
        IStringLocalizer<ReportDesignDocumentBuilder> stringLocalizer)
    {
        _formatter = formatter;
        S = stringLocalizer;
    }

    /// <summary>
    /// Checks that each visual refers to result columns that exist and suit it.
    /// </summary>
    /// <param name="visuals">The visuals.</param>
    /// <param name="columns">The result columns of the query.</param>
    /// <returns>The problems found.</returns>
    public IReadOnlyList<string> Validate(IEnumerable<ReportVisualDefinition> visuals, IReadOnlyList<ReportResultColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var errors = new List<string>();
        var byId = columns.ToDictionary(column => column.Id, StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var visual in visuals ?? [])
        {
            if (visual is null || string.IsNullOrEmpty(visual.Id) || !ids.Add(visual.Id))
            {
                errors.Add(S["Every visual needs a unique identifier."]);

                continue;
            }

            var name = string.IsNullOrWhiteSpace(visual.Title) ? visual.Type.ToString() : visual.Title;

            var lineColumnIds = (visual.ReferenceLines ?? []).Select(line => line?.ColumnId);

            foreach (var columnId in visual.ColumnIds.Concat(visual.ValueColumnIds).Append(visual.CategoryColumnId).Append(visual.SeriesColumnId).Concat(lineColumnIds))
            {
                if (!string.IsNullOrEmpty(columnId) && !byId.ContainsKey(columnId))
                {
                    errors.Add(S["The visual '{0}' refers to a column that no longer exists.", name]);

                    break;
                }
            }

            switch (visual.Type)
            {
                case ReportVisualType.Chart:
                    if (string.IsNullOrEmpty(visual.CategoryColumnId))
                    {
                        errors.Add(S["The chart '{0}' needs a category column.", name]);
                    }

                    if (visual.ValueColumnIds.Count == 0)
                    {
                        errors.Add(S["The chart '{0}' needs at least one value column.", name]);
                    }

                    foreach (var line in visual.ReferenceLines ?? [])
                    {
                        if (line is null)
                        {
                            continue;
                        }

                        if (line.Kind == ReportReferenceLineKind.Constant
                            ? line.Value is null
                            : !visual.ValueColumnIds.Contains(line.ColumnId ?? string.Empty, StringComparer.Ordinal))
                        {
                            errors.Add(S["Each reference line of the chart '{0}' needs a value, or one of the chart's value columns to compute it from.", name]);

                            break;
                        }
                    }

                    break;

                case ReportVisualType.Metrics:
                    if (visual.ValueColumnIds.Count == 0)
                    {
                        errors.Add(S["The metrics '{0}' need at least one value column.", name]);
                    }

                    break;

                case ReportVisualType.Pivot:
                    if (visual.ColumnIds.Count == 0 || string.IsNullOrEmpty(visual.SeriesColumnId) || visual.ValueColumnIds.Count == 0)
                    {
                        errors.Add(S["The pivot table '{0}' needs row columns, a column to spread across, and a value column.", name]);
                    }

                    break;
            }

            foreach (var columnId in visual.ValueColumnIds.Where(byId.ContainsKey))
            {
                if (visual.Type != ReportVisualType.Table && !DataValues.IsNumeric(byId[columnId].DataType))
                {
                    errors.Add(S["The visual '{0}' plots the column '{1}', which is not a number.", name, byId[columnId].Label]);
                }
            }

            if (!string.IsNullOrEmpty(visual.Action?.ColumnId) && !IsClickable(visual, byId.GetValueOrDefault(visual.Action.ColumnId)))
            {
                errors.Add(S["Clicking the visual '{0}' cannot filter other visuals: pick a column it shows that groups its data, such as a table column or a chart's categories.", name]);
            }
        }

        return errors;
    }

    // Whether clicking a visual can pick a value of a column: a table that shows the column, or a chart whose categories
    // or series it is. The column must group the data rather than sum it.
    private static bool IsClickable(ReportVisualDefinition visual, ReportResultColumn column)
    {
        if (column is null || column.IsMeasure)
        {
            return false;
        }

        return visual.Type switch
        {
            ReportVisualType.Table => visual.ColumnIds.Count == 0 ? !column.Hidden : visual.ColumnIds.Contains(column.Id, StringComparer.Ordinal),
            ReportVisualType.Chart => column.Id == visual.CategoryColumnId || column.Id == visual.SeriesColumnId,
            _ => false,
        };
    }

    /// <summary>
    /// Lists the groupings the visuals regroup the result by (see <see cref="ReportQueryResult.Regroup"/>), for
    /// <see cref="ReportQueryExecutionContext.Groupings"/>: an aggregated result is regrouped while the query runs.
    /// </summary>
    /// <param name="visuals">The visuals. When empty, a single table of the result is shown.</param>
    /// <param name="columns">The columns of the result.</param>
    /// <returns>The identifiers of the dimension columns of each grouping; an empty list for the grand total.</returns>
    public static IReadOnlyList<IReadOnlyList<string>> GetGroupings(IEnumerable<ReportVisualDefinition> visuals, IReadOnlyList<ReportResultColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var isAggregated = columns.Any(column => column.IsMeasure);
        var groupings = new List<IReadOnlyList<string>>();
        var list = (visuals ?? []).Where(visual => visual is not null).ToList();

        string IdOf(string id) => columns.Any(column => column.Id == id) ? id : null;

        if (list.Count == 0)
        {
            if (isAggregated)
            {
                groupings.AddRange(GetShelfLayout(DefaultTable(isAggregated), columns)?.Groupings ?? [[]]);
            }

            return groupings;
        }

        foreach (var visual in list)
        {
            switch (visual.Type)
            {
                case ReportVisualType.Metrics:
                    groupings.Add([]);
                    break;

                case ReportVisualType.Chart:
                    var category = IdOf(visual.CategoryColumnId);
                    var series = IdOf(visual.SeriesColumnId);

                    if (category is not null)
                    {
                        groupings.Add(series is not null && series != category ? [category, series] : [category]);
                    }

                    break;

                case ReportVisualType.Pivot:
                    groupings.AddRange(GetPivotLayout(visual, columns)?.Groupings ?? []);

                    break;

                default:
                    if (isAggregated && GetShelfLayout(visual, columns) is { } layout)
                    {
                        groupings.AddRange(layout.Groupings);

                        break;
                    }

                    var tableGrouping = GetTableGrouping(visual, columns);

                    if (tableGrouping is not null)
                    {
                        groupings.Add(tableGrouping);
                    }

                    if (visual.ShowTotals)
                    {
                        groupings.Add([]);
                    }

                    if (visual.ShowSubtotals && isAggregated)
                    {
                        // A subtotal after each group of the leading dimensions shown: every shown dimension but the last.
                        var shown = visual.ColumnIds.Count > 0
                            ? visual.ColumnIds.Select(id => columns.FirstOrDefault(column => column.Id == id)).Where(column => column is not null)
                            : columns.Where(column => !column.Hidden);
                        var dimensions = shown.Where(column => !column.IsMeasure).Select(column => column.Id).ToArray();

                        for (var level = 1; level < dimensions.Length; level++)
                        {
                            groupings.Add(dimensions[..level]);
                        }
                    }

                    break;
            }
        }

        return groupings;
    }

    /// <summary>
    /// Builds the report document.
    /// </summary>
    /// <param name="title">The report title.</param>
    /// <param name="visuals">The visuals, in display order. When empty, a single table of the result is shown.</param>
    /// <param name="result">The query result.</param>
    /// <param name="selections">
    /// The values picked in the visuals people can click, as keys by visual identifier, highlighted in those visuals.
    /// </param>
    /// <returns>The document.</returns>
    public ReportDocument Build(
        string title,
        IEnumerable<ReportVisualDefinition> visuals,
        ReportQueryResult result,
        IReadOnlyDictionary<string, IList<string>> selections = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var document = new ReportDocument
        {
            Title = title,
        };

        var list = (visuals ?? []).Where(visual => visual is not null).ToList();

        if (list.Count == 0)
        {
            list.Add(DefaultTable(result.IsAggregated));
        }

        foreach (var visual in list)
        {
            var section = visual.Type switch
            {
                ReportVisualType.Chart => BuildChart(visual, result),
                ReportVisualType.Metrics => BuildMetrics(visual, result),
                ReportVisualType.Pivot => BuildPivot(visual, result),
                _ => BuildTable(visual, result),
            };

            if (section is null)
            {
                continue;
            }

            section.Width = Math.Clamp(visual.Width, 1, 12);
            section.Id = visual.Id;
            section.ContainerId = visual.ContainerId;
            section.Selection = Selection(visual, section, result, selections);
            document.Add(section);
        }

        return document;
    }

    // The table a report without visuals shows: the result as laid out on the shelves, with totals when it is grouped.
    private static ReportVisualDefinition DefaultTable(bool isAggregated)
    {
        return new ReportVisualDefinition
        {
            Id = "table",
            Type = ReportVisualType.Table,
            ShowTotals = isAggregated,
        };
    }

    // What picking a value in a visual filters, when the section built for it carries the keys of the values it shows.
    private static ReportSectionSelection Selection(
        ReportVisualDefinition visual,
        ReportSection section,
        ReportQueryResult result,
        IReadOnlyDictionary<string, IList<string>> selections)
    {
        var targets = (visual.Action?.Targets ?? [])
            .Select(target => target?.VisualId)
            .Where(id => !string.IsNullOrEmpty(id) && id != visual.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var hasKeys = section.Rows.Any(row => row.Key is not null) || section.Chart?.Keys is not null || section.Chart?.SeriesKeys is not null;

        if (targets.Count == 0 || !hasKeys)
        {
            return null;
        }

        var keys = selections is not null && selections.TryGetValue(visual.Id ?? string.Empty, out var picked) ? picked : [];

        return new ReportSectionSelection
        {
            TargetIds = targets,
            SelectedKeys = [.. keys],
            SelectedLabels = [.. keys.Select(key => Label(visual, section, result, key) ?? key)],
        };
    }

    // How a section shows a value picked in it: the cell of its picked column, or the chart's label for it.
    private static string Label(ReportVisualDefinition visual, ReportSection section, ReportQueryResult result, string key)
    {
        if (section.Chart is { } chart)
        {
            var index = chart.Keys?.IndexOf(key) ?? -1;

            if (index >= 0 && index < chart.Labels.Count)
            {
                return chart.Labels[index];
            }

            index = chart.SeriesKeys?.IndexOf(key) ?? -1;

            return index >= 0 && index < chart.Datasets.Count ? chart.Datasets[index].Label : null;
        }

        var position = Array.IndexOf(TableIndexes(visual, result), ActionIndex(visual, result));
        var row = section.Rows.FirstOrDefault(candidate => candidate.Key == key);

        return position >= 0 && row is not null && position < row.Cells.Count ? row.Cells[position] : null;
    }

    // The positions in the result of the columns a table shows: those it lists, or every column the result doesn't hide,
    // those of the Rows shelf first.
    private static int[] TableIndexes(ReportVisualDefinition visual, ReportQueryResult result)
    {
        return visual.ColumnIds.Count > 0
            ? visual.ColumnIds.Select(result.IndexOf).Where(index => index >= 0).ToArray()
            : Enumerable.Range(0, result.Columns.Count)
                .Where(index => !result.Columns[index].Hidden)
                .OrderBy(index => result.Columns[index].Shelf == ReportColumnShelf.Rows ? 0 : 1)
                .ToArray();
    }

    // The position of the column clicking a visual picks, or -1 when clicking it picks nothing.
    private static int ActionIndex(ReportVisualDefinition visual, ReportQueryResult result)
    {
        var columnId = visual.Action?.ColumnId;

        if (string.IsNullOrEmpty(columnId) || (visual.Action.Targets?.Count ?? 0) == 0)
        {
            return -1;
        }

        var index = result.IndexOf(columnId);

        return index >= 0 && IsClickable(visual, result.Columns[index]) ? index : -1;
    }

    private ReportRow DetailRow(object[] values, ReportQueryResult result, int[] indexes, int keyIndex, ReportColumnColorizer[] colorizers)
    {
        var row = new ReportRow(indexes.Select(index => FormatShown(values[index], result.Columns[index], IsCalculated(result, index))).ToList())
        {
            Key = keyIndex >= 0 ? ReportSelectionKeys.ToKey(values[keyIndex]) : null,
        };

        for (var position = 0; position < indexes.Length; position++)
        {
            var style = colorizers[position]?.StyleOf(values[indexes[position]]);

            if (style is not null)
            {
                row.WithCellStyle(position, style);
            }
        }

        return row;
    }

    /// <summary>
    /// Adds to a document the containers its sections are placed in, in the order their first sections appear. A section
    /// placed in a container the report doesn't have is shown on its own.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="containers">The report's containers.</param>
    public static void AddContainers(ReportDocument document, IEnumerable<ReportContainerDefinition> containers)
    {
        ArgumentNullException.ThrowIfNull(document);

        var byId = (containers ?? [])
            .Where(container => !string.IsNullOrEmpty(container?.Id))
            .DistinctBy(container => container.Id, StringComparer.Ordinal)
            .ToDictionary(container => container.Id, StringComparer.Ordinal);

        document.Containers.Clear();

        foreach (var section in document.Sections)
        {
            if (string.IsNullOrEmpty(section.ContainerId) || document.Containers.Any(added => added.Id == section.ContainerId))
            {
                continue;
            }

            if (!byId.TryGetValue(section.ContainerId, out var container))
            {
                section.ContainerId = null;

                continue;
            }

            document.Containers.Add(new ReportContainer
            {
                Id = container.Id,
                Title = container.Title,
                Width = Math.Clamp(container.Width, 1, 12),
                Height = Math.Clamp(container.Height, 0, ReportContainerDefinition.MaxHeight),
            });
        }
    }

    // The dimension columns a table groups the result by, when it lists columns and leaves out some dimensions of an
    // aggregated result: the table then has one row per combination of the dimensions it shows, with its measures
    // computed over every row of that combination, instead of repeating those rows once per hidden dimension value.
    // Null when the table shows the result as it is.
    private static string[] GetTableGrouping(ReportVisualDefinition visual, IReadOnlyList<ReportResultColumn> columns)
    {
        if (visual.ColumnIds.Count == 0 || !columns.Any(column => column.IsMeasure))
        {
            return null;
        }

        var shown = visual.ColumnIds
            .Distinct(StringComparer.Ordinal)
            .Select(id => columns.FirstOrDefault(column => column.Id == id))
            .Where(column => column is { IsMeasure: false })
            .Select(column => column.Id)
            .ToArray();

        return shown.Length < columns.Count(column => !column.IsMeasure) ? shown : null;
    }

    private ReportSection BuildTable(ReportVisualDefinition visual, ReportQueryResult result)
    {
        if (result.IsAggregated && GetShelfLayout(visual, result.Columns) is { } layout)
        {
            return BuildCrosstab(visual, result, layout);
        }

        var grouping = GetTableGrouping(visual, result.Columns);

        if (grouping is not null)
        {
            var groups = result.Regroup(grouping);
            var original = result;

            // The regrouped rows are the table's result; subtotals and totals still regroup the rows behind the original.
            result = new ReportQueryResult(
                original.Columns,
                groups.Select(group => group.Values).ToList(),
                isAggregated: true,
                positions => original.Regroup(positions.Select(position => original.Columns[position].Id)))
            {
                Today = original.Today,
            };
        }

        var indexes = TableIndexes(visual, result);

        if (indexes.Length == 0)
        {
            return null;
        }

        var keyIndex = ActionIndex(visual, result);
        var withSubtotals = visual.ShowSubtotals && result.IsAggregated;
        var shown = ShownRows(result, indexes, withSubtotals);
        var colorizers = indexes
            .Select(index => ReportColumnColorizer.Create(ShownColumn(result.Columns[index], IsCalculated(result, index)), shown.Select(values => values[index]), result.Today))
            .ToArray();

        var columns = indexes
            .Select(index => new ReportColumn(result.Columns[index].Label, Align(result.Columns[index]), ReportColumnColorizer.HeaderStyleOf(result.Columns[index])))
            .ToList();
        var rows = withSubtotals
            ? BuildRowsWithSubtotals(result, shown, indexes, keyIndex, colorizers)
            : shown
                .Select(values => DetailRow(values, result, indexes, keyIndex, colorizers))
                .ToList();

        if (visual.ShowTotals && indexes.Any(index => IsTotalled(result, index)))
        {
            var totals = result.GetTotals();
            var cells = indexes
                .Select(index => IsTotalled(result, index) ? _formatter.Format(totals[index], result.Columns[index]) : string.Empty)
                .ToList();

            if (!IsTotalled(result, indexes[0]))
            {
                cells[0] = S["Total"];
            }

            rows.Add(new ReportRow(cells, ReportRowKind.GrandTotal));
        }

        return ReportSection.ForTable(visual.Title, columns, rows);
    }

    // The rows of a grouped table with a subtotal after each group of its leading dimensions (every shown dimension but
    // the last). Rows are kept together by those dimensions, keeping their order within each group. Each subtotal is
    // the result regrouped by the dimensions up to its level, so averages and distinct counts stay correct.
    private List<ReportRow> BuildRowsWithSubtotals(ReportQueryResult result, IReadOnlyList<object[]> detailRows, int[] indexes, int keyIndex, ReportColumnColorizer[] colorizers)
    {
        var dimensions = indexes.Where(index => !result.Columns[index].IsMeasure).ToArray();
        var levels = dimensions.Length - 1;

        if (levels < 1)
        {
            return detailRows
                .Select(values => DetailRow(values, result, indexes, keyIndex, colorizers))
                .ToList();
        }

        string Key(object[] values, int level)
        {
            return DataKeys.Build(values, dimensions[..(level + 1)], ignoreCase: true);
        }

        // The subtotals of each level, by the values of the dimensions up to it.
        var subtotals = new Dictionary<string, object[]>[levels];

        for (var level = 0; level < levels; level++)
        {
            var ids = dimensions.Take(level + 1).Select(index => result.Columns[index].Id).ToArray();
            var current = level;

            subtotals[level] = result.Regroup(ids)
                .GroupBy(group => Key(group.Values, current), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Values, StringComparer.Ordinal);
        }

        // Keep each group together, in the order its first row appears, without reordering rows within it: stable sorts
        // from the innermost level out leave the rows ordered by their outermost group first.
        var ordered = detailRows
            .Select((values, position) => (values, position))
            .ToList();

        for (var level = levels - 1; level >= 0; level--)
        {
            var current = level;
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var position = 0; position < ordered.Count; position++)
            {
                positions.TryAdd(Key(ordered[position].values, current), position);
            }

            ordered = ordered
                .Select((entry, position) => (entry, position))
                .OrderBy(item => positions[Key(item.entry.values, current)])
                .ThenBy(item => item.position)
                .Select(item => item.entry)
                .ToList();
        }

        var rows = new List<ReportRow>();
        var previous = default(object[]);

        void AddSubtotals(object[] values, int fromLevel)
        {
            for (var level = levels - 1; level >= fromLevel; level--)
            {
                if (!subtotals[level].TryGetValue(Key(values, level), out var totals))
                {
                    continue;
                }

                var cells = new List<string>(indexes.Length);

                foreach (var index in indexes)
                {
                    var position = Array.IndexOf(dimensions, index);

                    cells.Add(position == level
                        ? S["{0} total", _formatter.Format(values[index], result.Columns[index])].Value
                        : position >= 0 ? string.Empty : _formatter.Format(totals[index], result.Columns[index]));
                }

                rows.Add(new ReportRow(cells, ReportRowKind.Subtotal));
            }
        }

        foreach (var (values, _) in ordered)
        {
            if (previous is not null)
            {
                // The outermost level whose group changes closes that group and every group inside it.
                var changed = Enumerable.Range(0, levels).FirstOrDefault(level => Key(previous, level) != Key(values, level), -1);

                if (changed >= 0)
                {
                    AddSubtotals(previous, changed);
                }
            }

            rows.Add(DetailRow(values, result, indexes, keyIndex, colorizers));
            previous = values;
        }

        if (previous is not null)
        {
            AddSubtotals(previous, 0);
        }

        return rows;
    }

    // The detail rows of a table as it shows them: in its order, with each column shown as a quick table calculation
    // computed down them. Subtotals keep the groups of the leading dimensions together, so the calculations follow that
    // order. The result's own rows when no column is calculated.
    private static IReadOnlyList<object[]> ShownRows(ReportQueryResult result, int[] indexes, bool withSubtotals)
    {
        var calculated = indexes.Where(index => IsCalculated(result, index)).ToArray();

        if (calculated.Length == 0)
        {
            return result.Rows;
        }

        var dimensions = indexes.Where(index => !result.Columns[index].IsMeasure).ToArray();
        var rows = (withSubtotals && dimensions.Length > 1 ? KeepTogether([.. result.Rows], dimensions) : [.. result.Rows])
            .Select(values => (object[])values.Clone())
            .ToList();

        foreach (var index in calculated)
        {
            var grid = rows.Select(values => new[] { values[index] }).ToArray();

            ReportTableCalculations.Apply(grid, result.Columns[index].ShowAs, ReportShowAsDirection.Down);

            for (var row = 0; row < rows.Count; row++)
            {
                rows[row][index] = grid[row][0];
            }
        }

        return rows;
    }

    // Whether a column of a result shows its values as a quick table calculation: a number that a totals row totals.
    private static bool IsCalculated(ReportQueryResult result, int index)
    {
        return IsCalculable(result.Columns[index]) && IsTotalled(result, index);
    }

    private static bool IsCalculable(ReportResultColumn column)
    {
        return column.ShowAs != ReportShowAs.Value && DataValues.IsNumeric(column.DataType);
    }

    // The column as its calculated values read: percentages are decimals and ranks whole numbers, which color rules and
    // scales compare.
    private static ReportResultColumn ShownColumn(ReportResultColumn column, bool calculated)
    {
        if (!calculated)
        {
            return column;
        }

        return new ReportResultColumn
        {
            Id = column.Id,
            Field = column.Field,
            Label = column.Label,
            ValueLabel = column.ValueLabel,
            DataType = column.ShowAs switch
            {
                ReportShowAs.Rank => DataFieldType.Integer,
                ReportShowAs.PercentOfTotal or ReportShowAs.PercentDifference => DataFieldType.Decimal,
                _ => column.DataType,
            },
            IsMeasure = column.IsMeasure,
            Aggregate = column.Aggregate,
            Transform = column.Transform,
            Format = column.Format,
            Hidden = column.Hidden,
            Shelf = column.Shelf,
            Colors = column.Colors,
            ShowAs = column.ShowAs,
            ShowAsDirection = column.ShowAsDirection,
        };
    }

    // How a cell shows its value: a calculated value as its calculation reads, percentages as percentages (in the
    // column's format when that is a percent format) and ranks as whole numbers; any other value in the column's format.
    private string FormatShown(object value, ReportResultColumn column, bool calculated)
    {
        if (!calculated)
        {
            return _formatter.Format(value, column);
        }

        if (column.ShowAs == ReportShowAs.Rank)
        {
            return _formatter.Format(value, DataFieldType.Integer, "N0");
        }

        var text = ReportTableCalculations.IsPercent(column.ShowAs)
            ? _formatter.Format(value, DataFieldType.Decimal, PercentFormat(column))
            : _formatter.Format(value, column.DataType, column.Format);

        // A rise is marked with a plus sign, as a fall is with a minus sign.
        if (column.ShowAs is ReportShowAs.DifferenceFromPrevious or ReportShowAs.PercentDifference &&
            DataValues.Coerce(value, DataFieldType.Decimal) is decimal change && change > 0 && !text.StartsWith(CultureInfo.CurrentCulture.NumberFormat.PositiveSign, StringComparison.Ordinal))
        {
            return CultureInfo.CurrentCulture.NumberFormat.PositiveSign + text;
        }

        return text;
    }

    // The format of a percentage a column shows: its own format when it is a percent format, such as P0, or one decimal.
    private static string PercentFormat(ReportResultColumn column)
    {
        var format = column.Format?.Trim();

        return !string.IsNullOrEmpty(format) && (format[0] is 'P' or 'p' || format.Contains('%')) ? format : "P1";
    }

    private ReportSection BuildMetrics(ReportVisualDefinition visual, ReportQueryResult result)
    {
        var totals = result.GetTotals();
        var metrics = visual.ValueColumnIds
            .Select(result.IndexOf)
            .Where(index => index >= 0)
            .Select(index => new ReportMetric(result.Columns[index].Label, _formatter.Format(totals[index], result.Columns[index])))
            .ToList();

        return metrics.Count == 0 ? null : ReportSection.ForMetrics(visual.Title, metrics);
    }

    private ReportSection BuildChart(ReportVisualDefinition visual, ReportQueryResult result)
    {
        var categoryIndex = result.IndexOf(visual.CategoryColumnId);
        var valueIndexes = visual.ValueColumnIds.Select(result.IndexOf).Where(index => index >= 0).ToArray();

        if (categoryIndex < 0 || valueIndexes.Length == 0)
        {
            return null;
        }

        var categoryColumn = result.Columns[categoryIndex];
        var seriesIndex = result.IndexOf(visual.SeriesColumnId);
        var keyIndex = ActionIndex(visual, result);
        var chart = new ReportChart
        {
            Type = visual.ChartType,
            Stacked = visual.Stacked,
            ShowLegend = visual.ShowLegend,
        };

        // The values each value column plots, by category then series, for its reference lines.
        var plotted = new Dictionary<int, object[][]>();

        if (seriesIndex >= 0 && seriesIndex != categoryIndex)
        {
            var seriesColumn = result.Columns[seriesIndex];
            var valueIndex = valueIndexes[0];
            var groups = result.Regroup([categoryColumn.Id, seriesColumn.Id]);
            var categories = DistinctKeys(groups, categoryIndex);
            var series = DistinctKeys(groups, seriesIndex);
            var cells = groups.ToDictionary(
                group => Key(group.Values[categoryIndex]) + "\u001F" + Key(group.Values[seriesIndex]),
                group => group.Values[valueIndex],
                StringComparer.Ordinal);

            chart.Labels = categories.Select(category => _formatter.Format(category, categoryColumn)).ToList();

            if (keyIndex == categoryIndex)
            {
                chart.Keys = categories.Select(ReportSelectionKeys.ToKey).ToList();
            }
            else if (keyIndex == seriesIndex)
            {
                chart.SeriesKeys = series.Select(ReportSelectionKeys.ToKey).ToList();
            }

            // The categories down the side and the series across: a calculation down runs along the categories of each
            // series, and one across runs over the series of each category.
            var grid = categories
                .Select(category => series.Select(seriesValue => cells.GetValueOrDefault(Key(category) + "\u001F" + Key(seriesValue))).ToArray())
                .ToArray();
            var column = result.Columns[valueIndex];

            if (IsCalculable(column))
            {
                ReportTableCalculations.Apply(grid, column.ShowAs, column.ShowAsDirection);
            }

            plotted[valueIndex] = grid;

            for (var position = 0; position < series.Count; position++)
            {
                var current = position;

                chart.Datasets.Add(new ReportChartDataset(_formatter.Format(series[position], seriesColumn), grid.Select(row => ToPlotted(row[current], column))));
            }
        }
        else
        {
            var groups = result.Regroup([categoryColumn.Id]);

            chart.Labels = groups.Select(group => _formatter.Format(group.Values[categoryIndex], categoryColumn)).ToList();

            if (keyIndex == categoryIndex)
            {
                chart.Keys = groups.Select(group => ReportSelectionKeys.ToKey(group.Values[categoryIndex])).ToList();
            }

            foreach (var valueIndex in valueIndexes)
            {
                // Each value column is computed along the categories.
                var column = result.Columns[valueIndex];
                var grid = groups.Select(group => new[] { group.Values[valueIndex] }).ToArray();

                if (IsCalculable(column))
                {
                    ReportTableCalculations.Apply(grid, column.ShowAs, ReportShowAsDirection.Down);
                }

                plotted.TryAdd(valueIndex, grid);
                chart.Datasets.Add(new ReportChartDataset(column.Label, grid.Select(row => ToPlotted(row[0], column))));
            }
        }

        // Percentages are plotted in percent, on an axis read in percent, when every value column shows one.
        chart.PercentageScale = valueIndexes.All(index => IsCalculable(result.Columns[index]) && ReportTableCalculations.IsPercent(result.Columns[index].ShowAs));

        if (visual.ChartType is not (ReportChartType.Pie or ReportChartType.Doughnut))
        {
            AddReferenceLines(chart, visual, result, valueIndexes, plotted, seriesIndex >= 0 && seriesIndex != categoryIndex);
        }

        return ReportSection.ForChart(visual.Title, chart, visual.Width);
    }

    // Adds the reference lines of a chart: the average or median of the values it plots for a value column, or a
    // constant, labeled with the value formatted as the column shows it.
    private void AddReferenceLines(
        ReportChart chart,
        ReportVisualDefinition visual,
        ReportQueryResult result,
        int[] valueIndexes,
        Dictionary<int, object[][]> plotted,
        bool hasSeries)
    {
        foreach (var line in visual.ReferenceLines ?? [])
        {
            if (line is null)
            {
                continue;
            }

            var index = string.IsNullOrEmpty(line.ColumnId) ? -1 : result.IndexOf(line.ColumnId);
            var column = index >= 0 ? result.Columns[index] : null;

            // A line computed from a column is computed from what the chart plots of it.
            if (line.Kind != ReportReferenceLineKind.Constant && (column is null || !plotted.ContainsKey(index)))
            {
                continue;
            }

            var values = column is not null && plotted.TryGetValue(index, out var grid)
                ? grid.SelectMany(row => row).Where(value => value is not null).Select(value => ToPlotted(value, column, round: false)).ToList()
                : [];
            double? value = line.Kind switch
            {
                ReportReferenceLineKind.Average => values.Count > 0 ? values.Average() : null,
                ReportReferenceLineKind.Median => Median(values),
                _ => line.Value is { } constant ? (double)constant : null,
            };

            if (value is not { } at || !double.IsFinite(at))
            {
                continue;
            }

            var name = !string.IsNullOrWhiteSpace(line.Label)
                ? line.Label.Trim()
                : line.Kind switch
                {
                    ReportReferenceLineKind.Average => S["Average"].Value,
                    ReportReferenceLineKind.Median => S["Median"].Value,
                    _ => null,
                };
            var text = FormatLineValue(at, column, chart.PercentageScale);

            chart.ReferenceLines.Add(new ReportChartReferenceLine(name is null ? text : S["{0}: {1}", name, text].Value, at)
            {
                // A line of a column takes the color of its series, unless the column is split into several.
                DatasetIndex = !hasSeries && index >= 0 && Array.IndexOf(valueIndexes, index) is var position and >= 0 ? position : null,
            });
        }
    }

    // How a reference line shows its value: as a percentage on an axis read in percent, otherwise as the column shows
    // its values, with the decimals an average or median needs.
    private string FormatLineValue(double value, ReportResultColumn column, bool percentageScale)
    {
        var number = (decimal)Math.Round(value, 6);

        if (column is not null && IsCalculable(column) && ReportTableCalculations.IsPercent(column.ShowAs))
        {
            return _formatter.Format(number / 100, DataFieldType.Decimal, PercentFormat(column));
        }

        if (percentageScale)
        {
            return _formatter.Format(number / 100, DataFieldType.Decimal, "P1");
        }

        var format = column is null || column.ShowAs == ReportShowAs.Rank ? null : column.Format;

        return _formatter.Format(number, DataFieldType.Decimal, format);
    }

    private static double? Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    // The value a chart plots: a percentage in percent, rounded to hundredths of a percent unless a reference line is
    // computed from it; missing values as zero.
    private static double ToPlotted(object value, ReportResultColumn column, bool round = true)
    {
        var number = ToDouble(value);

        if (!IsCalculable(column) || !ReportTableCalculations.IsPercent(column.ShowAs))
        {
            return number;
        }

        return round ? Math.Round(number * 100, 2) : number * 100;
    }

    private ReportSection BuildPivot(ReportVisualDefinition visual, ReportQueryResult result)
    {
        var layout = GetPivotLayout(visual, result.Columns);

        return layout is null ? null : BuildCrosstab(visual, result, layout);
    }

    private static bool IsTotalled(ReportQueryResult result, int index)
    {
        var column = result.Columns[index];

        return result.IsAggregated
            ? column.IsMeasure
            : DataValues.IsNumeric(column.DataType) && column.Transform == DataFieldTransform.None;
    }

    private static ReportColumnAlign Align(ReportResultColumn column)
    {
        return DataValues.IsNumeric(column.DataType) && column.Transform is not (DataFieldTransform.Year or DataFieldTransform.DayOfWeek or DataFieldTransform.MonthOfYear)
            ? ReportColumnAlign.End
            : ReportColumnAlign.Start;
    }

    private static List<object> DistinctKeys(IReadOnlyList<ReportResultGroup> groups, int index)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var values = new List<object>();

        foreach (var group in groups)
        {
            if (seen.Add(Key(group.Values[index])))
            {
                values.Add(group.Values[index]);
            }
        }

        return values;
    }

    private static string RowKey(object[] values, int[] indexes)
    {
        return string.Join('\u001F', indexes.Select(index => Key(values[index])));
    }

    private static string Key(object value)
    {
        return DataValues.ToKey(value);
    }

    private static double ToDouble(object value)
    {
        return DataValues.Coerce(value, DataFieldType.Decimal) is decimal number
            ? (double)number
            : 0d;
    }
}
