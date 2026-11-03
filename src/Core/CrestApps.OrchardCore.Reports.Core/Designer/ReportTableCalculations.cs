using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Computes the quick table calculations of <see cref="ReportShowAs"/> over a grid of the values a visual shows: the
/// rows of a table or cross-tab by its columns, or the categories of a chart by its series.
/// </summary>
internal static class ReportTableCalculations
{
    /// <summary>
    /// Whether a calculation shows a percentage.
    /// </summary>
    public static bool IsPercent(ReportShowAs showAs)
    {
        return showAs is ReportShowAs.PercentOfTotal or ReportShowAs.PercentDifference;
    }

    /// <summary>
    /// Computes a calculation over a grid of values, in place. A value that is missing, or that is not a number, stays
    /// missing, except in a running total, which shows the sum so far once it has one.
    /// </summary>
    /// <param name="grid">The values, by row then column; rows may be of different lengths.</param>
    /// <param name="showAs">The calculation.</param>
    /// <param name="direction">The values each value is computed along.</param>
    public static void Apply(object[][] grid, ReportShowAs showAs, ReportShowAsDirection direction)
    {
        if (showAs == ReportShowAs.Value || grid.Length == 0)
        {
            return;
        }

        foreach (var partition in Partitions(grid, direction))
        {
            var values = partition.Select(cell => ToNumber(grid[cell.Row][cell.Column])).ToArray();
            var computed = Compute(values, showAs);

            for (var position = 0; position < partition.Count; position++)
            {
                var (row, column) = partition[position];

                grid[row][column] = computed[position];
            }
        }
    }

    // The cells each calculation runs over, in the order it runs: each column top to bottom, each row left to right, or
    // every cell row by row.
    private static List<List<(int Row, int Column)>> Partitions(object[][] grid, ReportShowAsDirection direction)
    {
        var partitions = new List<List<(int Row, int Column)>>();

        switch (direction)
        {
            case ReportShowAsDirection.Across:
                for (var row = 0; row < grid.Length; row++)
                {
                    partitions.Add(Enumerable.Range(0, grid[row].Length).Select(column => (row, column)).ToList());
                }

                break;

            case ReportShowAsDirection.Table:
                partitions.Add(Enumerable.Range(0, grid.Length)
                    .SelectMany(row => Enumerable.Range(0, grid[row].Length).Select(column => (row, column)))
                    .ToList());

                break;

            default:
                var width = grid.Max(row => row.Length);

                for (var column = 0; column < width; column++)
                {
                    var current = column;

                    partitions.Add(Enumerable.Range(0, grid.Length)
                        .Where(row => current < grid[row].Length)
                        .Select(row => (row, current))
                        .ToList());
                }

                break;
        }

        return partitions;
    }

    private static object[] Compute(decimal?[] values, ReportShowAs showAs)
    {
        var computed = new object[values.Length];

        switch (showAs)
        {
            case ReportShowAs.PercentOfTotal:
                var total = values.Sum(value => value ?? 0);

                for (var index = 0; index < values.Length; index++)
                {
                    computed[index] = values[index] is { } value && total != 0 ? value / total : null;
                }

                break;

            case ReportShowAs.RunningTotal:
                decimal? sum = null;

                for (var index = 0; index < values.Length; index++)
                {
                    if (values[index] is { } value)
                    {
                        sum = (sum ?? 0) + value;
                    }

                    computed[index] = sum;
                }

                break;

            case ReportShowAs.DifferenceFromPrevious:
                for (var index = 1; index < values.Length; index++)
                {
                    computed[index] = values[index] is { } value && values[index - 1] is { } previous ? value - previous : null;
                }

                break;

            case ReportShowAs.PercentDifference:
                for (var index = 1; index < values.Length; index++)
                {
                    computed[index] = values[index] is { } value && values[index - 1] is { } previous && previous != 0
                        ? (value - previous) / Math.Abs(previous)
                        : null;
                }

                break;

            case ReportShowAs.Rank:
                // Competition ranking: equal values share the best rank, and the next value skips the ranks they took.
                var ranks = new Dictionary<decimal, long>();
                var position = 0L;

                foreach (var value in values.Where(value => value.HasValue).Select(value => value.Value).OrderDescending())
                {
                    ranks.TryAdd(value, ++position);
                }

                for (var index = 0; index < values.Length; index++)
                {
                    computed[index] = values[index] is { } value ? ranks[value] : null;
                }

                break;

            default:
                return [.. values.Select(value => (object)value)];
        }

        return computed;
    }

    private static decimal? ToNumber(object value)
    {
        return DataValues.Coerce(value, DataFieldType.Decimal) is decimal number ? number : null;
    }
}
