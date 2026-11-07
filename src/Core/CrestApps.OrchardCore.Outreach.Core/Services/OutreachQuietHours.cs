using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Works out when a run whose channel observes quiet hours may send again, from the run's sending hours schedule.
/// </summary>
public sealed class OutreachQuietHours
{
    // How far ahead to look for the schedule's next opening.
    private static readonly TimeSpan _lookAhead = TimeSpan.FromDays(8);

    // When the schedule is closed and no opening is known, ask again after this long.
    private static readonly TimeSpan _retryWhenUnknown = TimeSpan.FromHours(1);

    private readonly IOpenHoursService _openHoursService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachQuietHours"/> class.
    /// </summary>
    /// <param name="openHoursServices">The open hours service, when Business Hours is enabled.</param>
    public OutreachQuietHours(IEnumerable<IOpenHoursService> openHoursServices)
    {
        _openHoursService = openHoursServices.FirstOrDefault();
    }

    /// <summary>
    /// Gets when the run may send again.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The UTC time the run's sending hours open next, or <see langword="null"/> when it may send now: it has no
    /// schedule, the schedule is open, or no source knows the schedule (a missing schedule never blocks sending).
    /// </returns>
    public async Task<DateTime?> GetResumeTimeAsync(OutreachRun run, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (_openHoursService is null || string.IsNullOrWhiteSpace(run.SendingHoursScheduleId))
        {
            return null;
        }

        var open = await _openHoursService.IsOpenAsync(run.SendingHoursScheduleId, nowUtc, cancellationToken: cancellationToken);

        if (open != false)
        {
            return null;
        }

        var windows = await _openHoursService.GetOpenWindowsAsync(run.SendingHoursScheduleId, nowUtc, nowUtc.Add(_lookAhead), cancellationToken: cancellationToken);
        var next = windows?
            .Where(window => window.StartUtc > nowUtc)
            .Select(window => (DateTime?)window.StartUtc)
            .Min();

        return next ?? nowUtc.Add(_retryWhenUnknown);
    }
}
