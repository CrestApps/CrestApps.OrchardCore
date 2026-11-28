using System.Globalization;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Turns what a report returns into results a model can read: headline numbers, tables as columns and rows, bars and
/// charts, with rows capped in number and the whole result capped in size. A report's values are already written in
/// the user's or the site's time zone; they are passed on as the report shows them.
/// </summary>
internal static class ReportToolResults
{
    /// <summary>
    /// The rows a table or a query returns when no limit is asked for.
    /// </summary>
    public const int DefaultRows = 200;

    /// <summary>
    /// The most rows a table or a query returns.
    /// </summary>
    public const int MaxRows = 1000;

    /// <summary>
    /// The most characters of cell text a result holds; rows beyond it are left out and the result says so.
    /// </summary>
    public const int MaxCharacters = 60_000;

    /// <summary>
    /// Reads the row limit a caller asked for, within the caps.
    /// </summary>
    /// <param name="requested">The limit asked for, or <see langword="null"/>.</param>
    /// <returns>The row limit.</returns>
    public static int GetRowLimit(int? requested)
        => Math.Clamp(requested ?? DefaultRows, 1, MaxRows);

    /// <summary>
    /// Flattens the sections of a report document.
    /// </summary>
    /// <param name="document">The document, with its display values resolved.</param>
    /// <param name="maxRows">The most rows of each table, bar list and chart.</param>
    /// <returns>The sections, and whether anything was left out.</returns>
    public static (List<object> Sections, bool Truncated) FlattenSections(ReportDocument document, int maxRows)
    {
        var sections = new List<object>();
        var budget = new Budget(MaxCharacters);
        var truncated = false;

        foreach (var section in document?.Sections ?? [])
        {
            if (section is null)
            {
                continue;
            }

            switch (section.Kind)
            {
                case ReportSectionKind.Metrics:
                    sections.Add(new
                    {
                        kind = "metrics",
                        title = Text(section.Title),
                        metrics = section.Metrics
                            .Where(metric => metric is not null)
                            .Select(metric => new
                            {
                                label = Text(metric.Label),
                                value = Text(metric.Value),
                                hint = Text(metric.Hint),
                            })
                            .ToArray(),
                    });
                    break;

                case ReportSectionKind.Table:
                {
                    var rows = new List<string[]>();
                    var detailRows = section.Rows.Where(row => row is not null).ToList();

                    foreach (var row in detailRows)
                    {
                        if (rows.Count >= maxRows || !budget.TryTake(row.Cells))
                        {
                            truncated = true;
                            break;
                        }

                        rows.Add(row.Cells.Select(Text).ToArray());
                    }

                    sections.Add(new
                    {
                        kind = "table",
                        title = Text(section.Title),
                        columns = section.Columns.Select(column => Text(column?.Label)).ToArray(),
                        rows,
                        rowCount = detailRows.Count,
                        truncated = rows.Count < detailRows.Count ? true : (bool?)null,
                        emptyMessage = detailRows.Count == 0 ? Text(section.EmptyMessage) : null,
                    });
                    break;
                }

                case ReportSectionKind.Bars:
                {
                    var bars = section.Bars.Where(bar => bar is not null).ToList();

                    truncated |= bars.Count > maxRows;
                    sections.Add(new
                    {
                        kind = "bars",
                        title = Text(section.Title),
                        bars = bars.Take(maxRows).Select(bar => new
                        {
                            label = Text(bar.Label),
                            value = Text(bar.Value),
                        }).ToArray(),
                        truncated = bars.Count > maxRows ? true : (bool?)null,
                    });
                    break;
                }

                case ReportSectionKind.Chart when section.Chart is not null:
                {
                    var labels = section.Chart.Labels ?? [];

                    truncated |= labels.Count > maxRows;
                    sections.Add(new
                    {
                        kind = "chart",
                        title = Text(section.Title),
                        chartType = section.Chart.Type.ToString(),
                        labels = labels.Take(maxRows).Select(Text).ToArray(),
                        series = (section.Chart.Datasets ?? [])
                            .Where(dataset => dataset is not null)
                            .Select(dataset => new
                            {
                                label = Text(dataset.Label),
                                values = (dataset.Values ?? []).Take(maxRows).ToArray(),
                            })
                            .ToArray(),
                        truncated = labels.Count > maxRows ? true : (bool?)null,
                    });
                    break;
                }
            }
        }

        return (sections, truncated);
    }

    /// <summary>
    /// Writes the rows of a designed report's or an ad hoc query's result by column, leaving out the columns the report
    /// hides from its viewers.
    /// </summary>
    /// <param name="columns">The result columns.</param>
    /// <param name="rows">The result rows, whose dates are already in the user's or the site's time zone.</param>
    /// <param name="maxRows">The most rows to return.</param>
    /// <returns>The column labels and data types, the rows, and whether rows were left out.</returns>
    public static (object[] Columns, List<object[]> Rows, bool Truncated) ToRows(IReadOnlyList<ReportResultColumn> columns, IReadOnlyList<object[]> rows, int maxRows)
    {
        var shown = (columns ?? [])
            .Select((column, index) => (Column: column, Index: index))
            .Where(entry => entry.Column is not null && !entry.Column.Hidden)
            .ToList();
        var budget = new Budget(MaxCharacters);
        var result = new List<object[]>();
        var all = rows ?? [];
        var truncated = false;

        foreach (var row in all)
        {
            if (row is null)
            {
                continue;
            }

            var values = shown.Select(entry => entry.Index < row.Length ? Value(row[entry.Index]) : null).ToArray();

            if (result.Count >= maxRows || !budget.TryTake(values.Select(value => value?.ToString())))
            {
                truncated = true;
                break;
            }

            result.Add(values);
        }

        var described = shown
            .Select(entry => (object)new
            {
                id = entry.Column.Id,
                label = entry.Column.Label ?? entry.Column.Id,
                dataType = entry.Column.DataType.ToString(),
                aggregate = entry.Column.Aggregate == global::OrchardCore.DataSources.DataAggregate.None ? null : entry.Column.Aggregate.ToString(),
            })
            .ToArray();

        return (described, result, truncated);
    }

    private static object Value(object value)
        => value switch
        {
            null => null,
            DateTime dateTime => dateTime.TimeOfDay == TimeSpan.Zero
                ? dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : dateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset offset => offset.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            bool or int or long or short or byte or double or float or decimal => value,
            string text => Text(text),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };

    // The text a report value shows: the text of a link, never its address.
    private static string Text(string value)
        => string.IsNullOrEmpty(value) ? value : ReportValue.GetText(value);

    private sealed class Budget
    {
        private int _remaining;

        public Budget(int characters)
        {
            _remaining = characters;
        }

        public bool TryTake(IEnumerable<string> cells)
        {
            var size = (cells ?? []).Sum(cell => (cell?.Length ?? 0) + 3);

            if (size > _remaining)
            {
                return false;
            }

            _remaining -= size;

            return true;
        }
    }
}
