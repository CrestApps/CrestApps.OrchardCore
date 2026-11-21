using CrestApps.OrchardCore.Surveys.Controls;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Summarizes number values in memory.
/// </summary>
public static class SurveyNumberSummary
{
    /// <summary>
    /// Summarizes values: count, average, lowest, highest, median and the count of each value.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The statistics.</returns>
    public static SurveyNumberStatistics Summarize(IEnumerable<decimal> values)
    {
        var sorted = (values ?? []).Order().ToList();
        var statistics = new SurveyNumberStatistics
        {
            Count = sorted.Count,
        };

        if (sorted.Count == 0)
        {
            return statistics;
        }

        statistics.Average = sorted.Average();
        statistics.Min = sorted[0];
        statistics.Max = sorted[sorted.Count - 1];
        statistics.Median = Median(sorted);

        foreach (var group in sorted.GroupBy(value => value))
        {
            statistics.Distribution[group.Key] = group.Count();
        }

        return statistics;
    }

    /// <summary>
    /// Gets the median of sorted values.
    /// </summary>
    /// <param name="sorted">The values in ascending order.</param>
    /// <returns>The median, or <see langword="null"/> when there are none.</returns>
    public static decimal? Median(IReadOnlyList<decimal> sorted)
    {
        if (sorted is null || sorted.Count == 0)
        {
            return null;
        }

        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
