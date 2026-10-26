using System.Globalization;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// The whole days a designed report covers, such as the period of a dashboard widget, in the site's time zone. It is
/// set on the first filter the report lets its viewers pick a date range with.
/// </summary>
public sealed class ReportRunPeriod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRunPeriod"/> class.
    /// </summary>
    /// <param name="firstDay">The first day covered, or <see langword="null"/> for no start.</param>
    /// <param name="lastDay">The last day covered, whole, or <see langword="null"/> for no end.</param>
    public ReportRunPeriod(DateOnly? firstDay, DateOnly? lastDay)
    {
        FirstDay = firstDay;
        LastDay = lastDay;
    }

    /// <summary>
    /// Gets the first day covered, or <see langword="null"/> for no start.
    /// </summary>
    public DateOnly? FirstDay { get; }

    /// <summary>
    /// Gets the last day covered, whole, or <see langword="null"/> for no end.
    /// </summary>
    public DateOnly? LastDay { get; }

    /// <summary>
    /// Creates a period from the first and last moments of a range in the site's time zone, such as the ones
    /// <c>ReportWidgetPeriods.Resolve</c> returns.
    /// </summary>
    /// <param name="from">The first moment, or <see langword="null"/> for no start.</param>
    /// <param name="to">The last moment, or <see langword="null"/> for no end.</param>
    /// <returns>The period of the days the range touches.</returns>
    public static ReportRunPeriod FromRange(DateTime? from, DateTime? to)
    {
        return new ReportRunPeriod(
            from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
            to.HasValue ? DateOnly.FromDateTime(to.Value) : null);
    }

    /// <summary>
    /// Writes the period as the values of a date range filter: the first and last days as <c>yyyy-MM-dd</c>, an open
    /// bound empty, and no values at all for a period with no bounds, which turns the filter off.
    /// </summary>
    /// <returns>The filter values.</returns>
    public IList<string> ToFilterValues()
    {
        if (FirstDay is null && LastDay is null)
        {
            return [];
        }

        return
        [
            FirstDay?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            LastDay?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
        ];
    }
}
