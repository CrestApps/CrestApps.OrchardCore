using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Resolves the period a coded report runs on from the bounds that were picked, the same way for the report page's date
/// range filter and for the AI tools that run a report.
/// </summary>
/// <remarks>
/// A report that declares a <see cref="ReportDescriptor.DefaultDateRange"/> starts on it, accepts open-ended ranges
/// ("on or after", "on or before") as they were picked, and offers <em>All Time</em>, which sets neither bound. A
/// report that declares none starts on today, and an empty bound is filled with today's.
/// </remarks>
public sealed class ReportDateRangeResolver
{
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly IReportManager _reportManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDateRangeResolver"/> class.
    /// </summary>
    /// <param name="clock">The clock used to compute the default reporting period.</param>
    /// <param name="localClock">The tenant local clock.</param>
    /// <param name="reportManager">The report manager, which describes the default range of each report.</param>
    public ReportDateRangeResolver(
        IClock clock,
        ILocalClock localClock,
        IReportManager reportManager)
    {
        _clock = clock;
        _localClock = localClock;
        _reportManager = reportManager;
    }

    /// <summary>
    /// Determines whether a report offers <em>All Time</em> and open-ended ranges, which only a report that declares a
    /// default range does.
    /// </summary>
    /// <param name="reportName">The technical name of the report.</param>
    /// <returns><see langword="true"/> when the report declares a default range.</returns>
    public bool AllowsAllTime(string reportName)
        => GetDefaultRange(reportName) is not null;

    /// <summary>
    /// Resolves the range a report runs on from the bounds that were picked.
    /// </summary>
    /// <param name="reportName">The technical name of the report.</param>
    /// <param name="fromUtc">The lower bound that was picked, in UTC, or <see langword="null"/>.</param>
    /// <param name="toUtc">The upper bound that was picked, in UTC, or <see langword="null"/>.</param>
    /// <param name="key">The key of the date range choice that was picked.</param>
    /// <returns>The range the report runs on.</returns>
    public async Task<ReportDateRange> NormalizeAsync(string reportName, DateTime? fromUtc, DateTime? toUtc, string key)
    {
        var defaultRange = GetDefaultRange(reportName);

        if (defaultRange is null)
        {
            // A report that declares no default starts on today, and runs on a full range.
            var (todayFromUtc, todayToUtc) = await ResolveUtcAsync(ReportDefaultDateRange.Today);

            fromUtc ??= todayFromUtc;
            toUtc ??= todayToUtc;
        }
        else if (string.Equals(key, ReportDefaultDateRange.AllTimeKey, StringComparison.Ordinal))
        {
            fromUtc = null;
            toUtc = null;
        }
        else if (fromUtc is null && toUtc is null)
        {
            (fromUtc, toUtc) = await ResolveUtcAsync(defaultRange);
            key = defaultRange.Key;
        }

        // A single bound is "on or after" or "on or before", so only a full range can be inverted.
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return new ReportDateRange
        {
            FromUtc = fromUtc.HasValue ? DateTime.SpecifyKind(fromUtc.Value, DateTimeKind.Utc) : null,
            ToUtc = toUtc.HasValue ? DateTime.SpecifyKind(toUtc.Value, DateTimeKind.Utc) : null,
            Key = key,
        };
    }

    /// <summary>
    /// Resolves the range a report runs on from whole days in the user's or the site's time zone: from the start of the
    /// first day to the end of the last one, as the date range picker sends them.
    /// </summary>
    /// <param name="reportName">The technical name of the report.</param>
    /// <param name="firstDay">The first day, or <see langword="null"/>.</param>
    /// <param name="lastDay">The last day, or <see langword="null"/>.</param>
    /// <param name="allTime">Whether every date is wanted; only a report that declares a default range accepts it.</param>
    /// <returns>The range the report runs on.</returns>
    public async Task<ReportDateRange> ResolveDaysAsync(string reportName, DateOnly? firstDay, DateOnly? lastDay, bool allTime)
    {
        if (allTime)
        {
            return await NormalizeAsync(reportName, null, null, ReportDefaultDateRange.AllTimeKey);
        }

        var fromUtc = firstDay.HasValue
            ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(firstDay.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified))
            : (DateTime?)null;
        var toUtc = lastDay.HasValue
            ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(lastDay.Value.ToDateTime(new TimeOnly(23, 59, 59)), DateTimeKind.Unspecified))
            : (DateTime?)null;

        return await NormalizeAsync(reportName, fromUtc, toUtc, firstDay.HasValue || lastDay.HasValue ? ReportDefaultDateRange.CustomKey : null);
    }

    private ReportDefaultDateRange GetDefaultRange(string reportName)
        => _reportManager.GetDescriptor(reportName)?.DefaultDateRange;

    private async Task<(DateTime? FromUtc, DateTime? ToUtc)> ResolveUtcAsync(ReportDefaultDateRange range)
    {
        var localNow = (await _localClock.ConvertToLocalAsync(_clock.UtcNow)).DateTime;
        var (fromLocal, toLocal) = range.Resolve(localNow);

        return (
            fromLocal.HasValue ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(fromLocal.Value, DateTimeKind.Unspecified)) : null,
            toLocal.HasValue ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(toLocal.Value, DateTimeKind.Unspecified)) : null);
    }
}
