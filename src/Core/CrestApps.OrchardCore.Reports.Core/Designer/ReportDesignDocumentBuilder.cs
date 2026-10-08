using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Turns a designed query result and its visuals into a <see cref="ReportDocument"/>, so designed reports render and
/// export through the same pipeline as every other report.
/// </summary>
public sealed class ReportDesignDocumentBuilder
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

            foreach (var columnId in visual.ColumnIds.Concat(visual.ValueColumnIds).Append(visual.CategoryColumnId).Append(visual.SeriesColumnId))
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
                if (visual.Type != ReportVisualType.Table && !ReportDataValues.IsNumeric(byId[columnId].DataType))
                {
                    errors.Add(S["The visual '{0}' plots the column '{1}', which is not a number.", name, byId[columnId].Label]);
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Builds the report document.
    /// </summary>
    /// <param name="title">The report title.</param>
    /// <param name="visuals">The visuals, in display order. When empty, a single table of the result is shown.</param>
    /// <param name="result">The query result.</param>
    /// <returns>The document.</returns>
    public ReportDocument Build(string title, IEnumerable<ReportVisualDefinition> visuals, ReportQueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var document = new ReportDocument
        {
            Title = title,
        };

        var list = (visuals ?? []).Where(visual => visual is not null).ToList();

        if (list.Count == 0)
        {
            list.Add(new ReportVisualDefinition
            {
                Id = "table",
                Type = ReportVisualType.Table,
                ShowTotals = result.IsAggregated,
            });
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
            document.Add(section);
        }

        return document;
    }

    private ReportSection BuildTable(ReportVisualDefinition visual, ReportQueryResult result)
    {
        var indexes = visual.ColumnIds.Count > 0
            ? visual.ColumnIds.Select(result.IndexOf).Where(index => index >= 0).ToArray()
            : Enumerable.Range(0, result.Columns.Count).Where(index => !result.Columns[index].Hidden).ToArray();

        if (indexes.Length == 0)
        {
            return null;
        }

        var columns = indexes
            .Select(index => new ReportColumn(result.Columns[index].Label, Align(result.Columns[index])))
            .ToList();
        var rows = result.Rows
            .Select(values => new ReportRow(indexes.Select(index => _formatter.Format(values[index], result.Columns[index])).ToList()))
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
        var chart = new ReportChart
        {
            Type = visual.ChartType,
            Stacked = visual.Stacked,
            ShowLegend = visual.ShowLegend,
        };

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

            foreach (var seriesValue in series)
            {
                var values = categories.Select(category => ToDouble(cells.GetValueOrDefault(Key(category) + "\u001F" + Key(seriesValue))));

                chart.Datasets.Add(new ReportChartDataset(_formatter.Format(seriesValue, seriesColumn), values));
            }
        }
        else
        {
            var groups = result.Regroup([categoryColumn.Id]);

            chart.Labels = groups.Select(group => _formatter.Format(group.Values[categoryIndex], categoryColumn)).ToList();

            foreach (var valueIndex in valueIndexes)
            {
                chart.Datasets.Add(new ReportChartDataset(result.Columns[valueIndex].Label, groups.Select(group => ToDouble(group.Values[valueIndex]))));
            }
        }

        return ReportSection.ForChart(visual.Title, chart, visual.Width);
    }

    private ReportSection BuildPivot(ReportVisualDefinition visual, ReportQueryResult result)
    {
        var rowIndexes = visual.ColumnIds.Select(result.IndexOf).Where(index => index >= 0).ToArray();
        var seriesIndex = result.IndexOf(visual.SeriesColumnId);
        var valueIndex = visual.ValueColumnIds.Select(result.IndexOf).FirstOrDefault(index => index >= 0, -1);

        if (rowIndexes.Length == 0 || seriesIndex < 0 || valueIndex < 0)
        {
            return null;
        }

        var rowIds = rowIndexes.Select(index => result.Columns[index].Id).ToArray();
        var seriesColumn = result.Columns[seriesIndex];
        var valueColumn = result.Columns[valueIndex];
        var cells = result.Regroup([.. rowIds, seriesColumn.Id]);
        var series = DistinctKeys(cells, seriesIndex);
        var rowGroups = result.Regroup(rowIds);
        var lookup = cells.ToDictionary(
            group => RowKey(group.Values, rowIndexes) + "\u001E" + Key(group.Values[seriesIndex]),
            group => group.Values[valueIndex],
            StringComparer.Ordinal);

        var columns = rowIndexes
            .Select(index => new ReportColumn(result.Columns[index].Label))
            .Concat(series.Select(value => new ReportColumn(_formatter.Format(value, seriesColumn), ReportColumnAlign.End)))
            .ToList();

        if (visual.ShowTotals)
        {
            columns.Add(new ReportColumn(S["Total"], ReportColumnAlign.End));
        }

        var rows = new List<ReportRow>();

        foreach (var group in rowGroups)
        {
            var rowKey = RowKey(group.Values, rowIndexes);
            var row = rowIndexes.Select(index => _formatter.Format(group.Values[index], result.Columns[index])).ToList();

            row.AddRange(series.Select(value => _formatter.Format(lookup.GetValueOrDefault(rowKey + "\u001E" + Key(value)), valueColumn)));

            if (visual.ShowTotals)
            {
                row.Add(_formatter.Format(group.Values[valueIndex], valueColumn));
            }

            rows.Add(new ReportRow(row));
        }

        if (visual.ShowTotals)
        {
            var seriesTotals = result.Regroup([seriesColumn.Id]).ToDictionary(group => Key(group.Values[seriesIndex]), group => group.Values[valueIndex], StringComparer.Ordinal);
            var totals = rowIndexes.Select(_ => string.Empty).ToList();

            totals[0] = S["Total"];
            totals.AddRange(series.Select(value => _formatter.Format(seriesTotals.GetValueOrDefault(Key(value)), valueColumn)));
            totals.Add(_formatter.Format(result.GetTotals()[valueIndex], valueColumn));
            rows.Add(new ReportRow(totals, ReportRowKind.GrandTotal));
        }

        return ReportSection.ForTable(visual.Title, columns, rows);
    }

    private static bool IsTotalled(ReportQueryResult result, int index)
    {
        var column = result.Columns[index];

        return result.IsAggregated
            ? column.IsMeasure
            : ReportDataValues.IsNumeric(column.DataType) && column.Transform == ReportFieldTransform.None;
    }

    private static ReportColumnAlign Align(ReportResultColumn column)
    {
        return ReportDataValues.IsNumeric(column.DataType) && column.Transform is not (ReportFieldTransform.Year or ReportFieldTransform.DayOfWeek or ReportFieldTransform.MonthOfYear)
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
        return ReportDataValues.ToKey(value);
    }

    private static double ToDouble(object value)
    {
        return ReportDataValues.Coerce(value, ReportDataType.Decimal) is decimal number
            ? (double)number
            : 0d;
    }
}
