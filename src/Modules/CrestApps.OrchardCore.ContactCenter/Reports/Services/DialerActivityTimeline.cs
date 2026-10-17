using System.Globalization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// The hours, in the tenant's time zone, the dialer activity report counts its calls and decisions in.
/// </summary>
/// <remarks>
/// A period longer than <see cref="MaxHourlyDays"/> days is counted by day instead, because a chart of more hours than
/// that has more points than it has width to draw them in.
/// </remarks>
internal sealed class DialerActivityTimeline
{
    /// <summary>
    /// The longest period, in days, counted by the hour.
    /// </summary>
    public const int MaxHourlyDays = 31;

    private readonly ReportTimeZone _timeZone;
    private readonly Dictionary<DateTime, int> _positions;

    private DialerActivityTimeline(ReportTimeZone timeZone, bool hourly, List<DateTime> bucketStarts)
    {
        _timeZone = timeZone;
        Hourly = hourly;
        BucketStarts = bucketStarts;
        _positions = new Dictionary<DateTime, int>(bucketStarts.Count);

        for (var position = 0; position < bucketStarts.Count; position++)
        {
            _positions.TryAdd(bucketStarts[position], position);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the period is counted by the hour rather than by the day.
    /// </summary>
    public bool Hourly { get; }

    /// <summary>
    /// Gets the local start of each hour, or day, in the period.
    /// </summary>
    public IReadOnlyList<DateTime> BucketStarts { get; }

    /// <summary>
    /// Gets the label of each hour, or day, in the period.
    /// </summary>
    public IEnumerable<string> Labels
        => BucketStarts.Select(start => start.ToString(Hourly ? "MMM d HH:00" : "MMM d", CultureInfo.InvariantCulture));

    /// <summary>
    /// Creates the timeline of a period.
    /// </summary>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period.</param>
    /// <param name="timeZone">The time zone the hours are in.</param>
    /// <returns>The timeline.</returns>
    public static DialerActivityTimeline Create(DateTime fromUtc, DateTime toUtc, ReportTimeZone timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        if (toUtc < fromUtc)
        {
            toUtc = fromUtc;
        }

        var hourly = toUtc - fromUtc <= TimeSpan.FromDays(MaxHourlyDays);
        var step = hourly ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        var last = Floor(timeZone.ToLocal(toUtc), hourly);
        var starts = new List<DateTime>();

        for (var start = Floor(timeZone.ToLocal(fromUtc), hourly); start <= last; start = start.Add(step))
        {
            starts.Add(start);
        }

        return new DialerActivityTimeline(timeZone, hourly, starts);
    }

    /// <summary>
    /// Counts occurrences into the hours, or days, they happened in. An occurrence outside the period is not counted.
    /// </summary>
    /// <param name="occurrences">When each occurrence happened and how many decisions or calls it stands for.</param>
    /// <returns>The count of each hour, or day, in the order of <see cref="BucketStarts"/>.</returns>
    public double[] Count(IEnumerable<(DateTime OccurredUtc, int Weight)> occurrences)
    {
        var counts = new double[BucketStarts.Count];

        if (occurrences is null)
        {
            return counts;
        }

        foreach (var (occurredUtc, weight) in occurrences)
        {
            if (_positions.TryGetValue(Floor(_timeZone.ToLocal(occurredUtc), Hourly), out var position))
            {
                counts[position] += weight;
            }
        }

        return counts;
    }

    private static DateTime Floor(DateTime local, bool hourly)
        => hourly
            ? new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified)
            : local.Date;
}
