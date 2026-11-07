namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// A stretch of time in UTC, from a start to an end (exclusive). Use it for every interval of time instead of a
/// start/end pair or a tuple: busy and free time, open hours, coverage and availability.
/// </summary>
/// <param name="StartUtc">When it starts.</param>
/// <param name="EndUtc">When it ends (exclusive).</param>
public readonly record struct TimeWindow(DateTime StartUtc, DateTime EndUtc)
{
    /// <summary>
    /// Gets how long the window lasts; zero or less when it is empty.
    /// </summary>
    public TimeSpan Duration => EndUtc - StartUtc;

    /// <summary>
    /// Gets whether the window covers no time.
    /// </summary>
    public bool IsEmpty => EndUtc <= StartUtc;

    /// <summary>
    /// Gets whether an instant falls inside the window.
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <returns><see langword="true"/> when the instant is at or after the start and before the end.</returns>
    public bool Contains(DateTime utc)
        => utc >= StartUtc && utc < EndUtc;

    /// <summary>
    /// Gets whether the window shares some time with another.
    /// </summary>
    /// <param name="other">The other window.</param>
    /// <returns><see langword="true"/> when they overlap; windows that only touch don't.</returns>
    public bool Overlaps(TimeWindow other)
        => StartUtc < other.EndUtc && other.StartUtc < EndUtc;

    /// <summary>
    /// Gets the time the window shares with another.
    /// </summary>
    /// <param name="other">The other window.</param>
    /// <returns>The shared time, or <see langword="null"/> when they don't overlap.</returns>
    public TimeWindow? Intersect(TimeWindow other)
    {
        var start = StartUtc > other.StartUtc ? StartUtc : other.StartUtc;
        var end = EndUtc < other.EndUtc ? EndUtc : other.EndUtc;

        return end > start ? new TimeWindow(start, end) : null;
    }

    /// <summary>
    /// Sorts windows and joins those that overlap or touch. Empty windows are left out.
    /// </summary>
    /// <param name="windows">The windows.</param>
    /// <returns>The joined windows, in order.</returns>
    public static IReadOnlyList<TimeWindow> Merge(IEnumerable<TimeWindow> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var merged = new List<TimeWindow>();

        foreach (var window in windows.Where(window => !window.IsEmpty).OrderBy(window => window.StartUtc))
        {
            if (merged.Count > 0 && merged[^1].EndUtc >= window.StartUtc)
            {
                if (window.EndUtc > merged[^1].EndUtc)
                {
                    merged[^1] = merged[^1] with { EndUtc = window.EndUtc };
                }

                continue;
            }

            merged.Add(window);
        }

        return merged;
    }

    /// <summary>
    /// Takes holes out of windows, such as breaks and time off out of shifts.
    /// </summary>
    /// <param name="windows">The windows.</param>
    /// <param name="holes">The time to take out.</param>
    /// <returns>What is left of the windows, in order.</returns>
    public static IReadOnlyList<TimeWindow> Subtract(IEnumerable<TimeWindow> windows, IEnumerable<TimeWindow> holes)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(holes);

        var cuts = Merge(holes);
        var result = new List<TimeWindow>();

        foreach (var window in windows.Where(window => !window.IsEmpty).OrderBy(window => window.StartUtc))
        {
            var start = window.StartUtc;

            foreach (var cut in cuts)
            {
                if (cut.EndUtc <= start || cut.StartUtc >= window.EndUtc)
                {
                    continue;
                }

                if (cut.StartUtc > start)
                {
                    result.Add(new TimeWindow(start, cut.StartUtc));
                }

                start = cut.EndUtc > start ? cut.EndUtc : start;
            }

            if (start < window.EndUtc)
            {
                result.Add(new TimeWindow(start, window.EndUtc));
            }
        }

        return result;
    }

    /// <summary>
    /// Cuts windows to a range, leaving out the parts outside it.
    /// </summary>
    /// <param name="windows">The windows.</param>
    /// <param name="range">The range.</param>
    /// <returns>The parts inside the range, in order.</returns>
    public static IReadOnlyList<TimeWindow> Clip(IEnumerable<TimeWindow> windows, TimeWindow range)
    {
        ArgumentNullException.ThrowIfNull(windows);

        return [.. windows
            .Select(window => window.Intersect(range))
            .Where(window => window.HasValue)
            .Select(window => window.Value)
            .OrderBy(window => window.StartUtc)];
    }
}
