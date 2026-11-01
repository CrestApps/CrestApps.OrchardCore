using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;

/// <summary>
/// A planning group's day as it happens: its rows, a reforecast of the rest of the day, and voluntary overtime and
/// time off offered to the staffing team.
/// </summary>
public sealed class IntradayService
{
    private readonly ISession _session;
    private readonly PlanningGroupManager _groupManager;
    private readonly ForecastService _forecastService;
    private readonly IIntervalStatisticsReader _statistics;
    private readonly ICoverageService _coverageService;
    private readonly IWorkerManager _workerManager;
    private readonly IShiftService _shiftService;
    private readonly IWorkerTimeZoneService _timeZones;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntradayService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="groupManager">The planning group manager.</param>
    /// <param name="forecastService">The forecast service.</param>
    /// <param name="statistics">The interval statistics.</param>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="shiftService">The shift service.</param>
    /// <param name="timeZones">The time zones.</param>
    /// <param name="clock">The clock.</param>
    public IntradayService(
        ISession session,
        PlanningGroupManager groupManager,
        ForecastService forecastService,
        IIntervalStatisticsReader statistics,
        ICoverageService coverageService,
        IWorkerManager workerManager,
        IShiftService shiftService,
        IWorkerTimeZoneService timeZones,
        IClock clock)
    {
        _session = session;
        _groupManager = groupManager;
        _forecastService = forecastService;
        _statistics = statistics;
        _coverageService = coverageService;
        _workerManager = workerManager;
        _shiftService = shiftService;
        _timeZones = timeZones;
        _clock = clock;
    }

    /// <summary>
    /// Gets the time zone of a planning group's team.
    /// </summary>
    /// <param name="group">The planning group.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The time zone.</returns>
    public Task<string> TimeZoneOfAsync(PlanningGroup group, CancellationToken cancellationToken = default)
        => _timeZones.GetTeamAsync(group?.TeamId, cancellationToken);

    /// <summary>
    /// Gets a planning group's day: for each 15-minute interval with a forecast, work or people, the forecast and
    /// actual work, the agents needed, scheduled and available, and the service level reached.
    /// </summary>
    /// <param name="group">The planning group.</param>
    /// <param name="date">The day, in the team's time zone.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The rows, in order.</returns>
    public async Task<IReadOnlyList<IntradayRow>> DayAsync(PlanningGroup group, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        var zone = await TimeZoneOfAsync(group, cancellationToken);
        var fromUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var now = _clock.UtcNow;
        var rows = new SortedDictionary<DateTime, IntradayRow>();

        IntradayRow RowAt(DateTime startUtc)
        {
            if (!rows.TryGetValue(startUtc, out var row))
            {
                row = new IntradayRow { StartUtc = startUtc, Elapsed = startUtc.AddMinutes(15) <= now };
                rows.Add(startUtc, row);
            }

            return row;
        }

        foreach (var interval in await _forecastService.ActiveIntervalsAsync(group.ItemId, fromUtc, toUtc, cancellationToken))
        {
            var row = RowAt(interval.StartUtc);

            row.ForecastVolume = interval.Volume;
            row.ForecastHandleSeconds = interval.AverageHandleSeconds;
            row.Required = interval.RequiredAgents;
            row.RequiredScheduled = interval.RequiredScheduled;
        }

        foreach (var interval in IntervalStatisticsRollup.Combine(
            (await _statistics.GetQueueIntervalsAsync(group.QueueIds, fromUtc, toUtc, cancellationToken)).Where(interval => group.Channel is null || interval.Channel == group.Channel),
            15).Select(interval => interval))
        {
            var row = RowAt(interval.IntervalStartUtc);

            row.ActualVolume = interval.Offered;
            row.ActualHandleSeconds = interval.Handled > 0 ? interval.AverageHandleSeconds : null;
            row.ServiceLevel = interval.ServiceLevel;
        }

        if (!string.IsNullOrEmpty(group.TeamId))
        {
            foreach (var interval in await _coverageService.ComputeAsync(new CoverageQuery { TeamId = group.TeamId, From = date, To = date, IntervalMinutes = 15 }, cancellationToken))
            {
                if (interval.Scheduled > 0 || rows.ContainsKey(interval.StartUtc))
                {
                    RowAt(interval.StartUtc).Scheduled = interval.Scheduled;
                }
            }

            var userIds = (await _workerManager.ListByTeamAsync(group.TeamId, date, cancellationToken))
                .Select(worker => worker.UserId)
                .Where(userId => !string.IsNullOrEmpty(userId))
                .ToArray();

            if (userIds.Length > 0)
            {
                foreach (var interval in (await _statistics.GetAgentIntervalsAsync(userIds, fromUtc, toUtc, cancellationToken)).GroupBy(interval => interval.IntervalStartUtc))
                {
                    var seconds = interval.Sum(agent => AdherenceCalculator.WorkStates.Sum(agent.SecondsIn));

                    RowAt(interval.Key).Available = Math.Round(seconds / 900d, 1);
                }
            }
        }

        return [.. rows.Values];
    }

    /// <summary>
    /// Scales the rest of the day's active forecast by how far the work so far is from it, damped by half.
    /// </summary>
    /// <param name="group">The planning group.</param>
    /// <param name="date">The day.</param>
    /// <param name="actor">Who asked.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The factor used and how many intervals were scaled.</returns>
    public async Task<(double Factor, int Intervals)> ReforecastAsync(PlanningGroup group, DateOnly date, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        var rows = await DayAsync(group, date, cancellationToken);
        var factor = IntradayCalculator.ReforecastFactor(rows);

        if (Math.Abs(factor - 1) < 0.005)
        {
            return (factor, 0);
        }

        var zone = await TimeZoneOfAsync(group, cancellationToken);
        var now = _clock.UtcNow;
        var next = IntervalStatisticsConstants.IntervalStart(now).AddMinutes(15);
        var dayEnd = CalendarTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var scaled = 0;

        foreach (var forecast in await _forecastService.ActiveAsync(group.ItemId, cancellationToken))
        {
            scaled += await _forecastService.ScaleAsync(forecast, next, dayEnd, factor, "Reforecast of the rest of the day", actor, cancellationToken);
        }

        return (factor, scaled);
    }

    /// <summary>
    /// Offers voluntary overtime or time off to the staffing team of a planning group.
    /// </summary>
    /// <param name="offer">The offer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The errors, or none when it was offered.</returns>
    public async Task<IReadOnlyList<string>> OfferAsync(IntradayOffer offer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var errors = new List<string>();
        var group = string.IsNullOrEmpty(offer.PlanningGroupId) ? null : await _groupManager.FindByIdAsync(offer.PlanningGroupId, cancellationToken);

        if (group is null || string.IsNullOrEmpty(group.TeamId))
        {
            errors.Add("Choose a planning group with a team.");
        }

        if (offer.EndUtc <= offer.StartUtc)
        {
            errors.Add("The offer must end after it starts.");
        }

        if (offer.EndUtc <= _clock.UtcNow)
        {
            errors.Add("The offer is already over.");
        }

        if (offer.Places < 1)
        {
            errors.Add("Offer at least one place.");
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        offer.ItemId ??= IdGenerator.GenerateId();
        offer.TeamId = group.TeamId;
        offer.Status = IntradayOfferStatus.Open;
        offer.CreatedUtc = _clock.UtcNow;

        await _session.SaveAsync(offer, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

        return errors;
    }

    /// <summary>
    /// Gets the offers of a planning group that start inside a stretch of time.
    /// </summary>
    /// <param name="groupId">The planning group.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The offers, in order.</returns>
    public async Task<IReadOnlyList<IntradayOffer>> ListAsync(string groupId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return [.. await _session.Query<IntradayOffer, IntradayOfferIndex>(index => index.PlanningGroupId == groupId && index.StartUtc >= fromUtc && index.StartUtc < toUtc, collection: WorkforceConstants.CollectionName)
            .OrderBy(index => index.StartUtc)
            .ListAsync(cancellationToken)];
    }

    /// <summary>
    /// Gets the open offers a worker can take: their teams', not over, not taken by them.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The offers, in order.</returns>
    public async Task<IReadOnlyList<IntradayOffer>> OpenForAsync(WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var now = _clock.UtcNow;
        var teamIds = worker.MembershipsOn(DateOnly.FromDateTime(now)).Select(membership => membership.TeamId).Where(id => !string.IsNullOrEmpty(id)).ToArray();

        if (teamIds.Length == 0)
        {
            return [];
        }

        var open = IntradayOfferStatus.Open.ToString();
        var offers = await _session.Query<IntradayOffer, IntradayOfferIndex>(index => index.TeamId.IsIn(teamIds) && index.Status == open && index.StartUtc >= now.AddHours(-12), collection: WorkforceConstants.CollectionName)
            .OrderBy(index => index.StartUtc)
            .ListAsync(cancellationToken);

        return [.. offers.Where(offer => offer.EndUtc > now && offer.Claims.All(claim => claim.WorkerId != worker.ItemId))];
    }

    /// <summary>
    /// Finds an offer.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The offer, or <see langword="null"/>.</returns>
    public Task<IntradayOffer> FindAsync(string id, CancellationToken cancellationToken = default)
        => _session.Query<IntradayOffer, IntradayOfferIndex>(index => index.ItemId == id, collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Closes an offer to new takers.
    /// </summary>
    /// <param name="offer">The offer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when it is saved.</returns>
    public async Task CloseAsync(IntradayOffer offer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offer);

        if (offer.Status == IntradayOfferStatus.Open)
        {
            offer.Status = IntradayOfferStatus.Closed;
            await _session.SaveAsync(offer, checkConcurrency: true, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Takes an offer for a worker: overtime extends their shift that ends as it starts or starts as it ends, and
    /// time off cuts the start or end of their shift. The change is the worker's, so it earns no predictability pay.
    /// </summary>
    /// <param name="offerId">The offer.</param>
    /// <param name="workerId">The worker.</param>
    /// <param name="actor">Who took it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The errors, or none when it was taken.</returns>
    public async Task<IReadOnlyList<string>> TakeAsync(string offerId, string workerId, string actor, CancellationToken cancellationToken = default)
    {
        var offer = string.IsNullOrEmpty(offerId) ? null : await FindAsync(offerId, cancellationToken);
        var worker = string.IsNullOrEmpty(workerId) ? null : await _workerManager.FindByIdAsync(workerId, cancellationToken);

        if (offer is null || worker is null)
        {
            return ["The offer or the worker doesn't exist."];
        }

        if (offer.Status != IntradayOfferStatus.Open || offer.EndUtc <= _clock.UtcNow)
        {
            return ["The offer is no longer open."];
        }

        if (offer.Claims.Any(claim => claim.WorkerId == worker.ItemId))
        {
            return ["You already took this offer."];
        }

        if (!worker.MembershipsOn(DateOnly.FromDateTime(offer.StartUtc)).Any(membership => membership.TeamId == offer.TeamId))
        {
            return ["The offer is for another team."];
        }

        var shifts = await _shiftService.ListWorkerShiftsAsync(worker.ItemId, offer.StartUtc.AddHours(-16), offer.EndUtc.AddHours(16), cancellationToken);
        Calendars.Models.CalendarEvent shift;
        DateTime start;
        DateTime end;

        if (offer.Kind == IntradayOfferKind.VoluntaryOvertime)
        {
            shift = shifts.FirstOrDefault(item => item.EndUtc == offer.StartUtc) ?? shifts.FirstOrDefault(item => item.StartUtc == offer.EndUtc);

            if (shift is null)
            {
                return ["Overtime is added to a shift that ends when it starts or starts when it ends, and you have no such shift."];
            }

            start = shift.EndUtc == offer.StartUtc ? shift.StartUtc : offer.StartUtc;
            end = shift.EndUtc == offer.StartUtc ? offer.EndUtc : shift.EndUtc;
        }
        else
        {
            shift = shifts.FirstOrDefault(item => item.EndUtc == offer.EndUtc && item.StartUtc < offer.StartUtc)
                ?? shifts.FirstOrDefault(item => item.StartUtc == offer.StartUtc && item.EndUtc > offer.EndUtc);

            if (shift is null)
            {
                return ["Time off is taken from the start or the end of a shift, and none of your shifts starts or ends with this offer."];
            }

            start = shift.EndUtc == offer.EndUtc ? shift.StartUtc : offer.EndUtc;
            end = shift.EndUtc == offer.EndUtc ? offer.StartUtc : shift.EndUtc;
        }

        var reason = offer.Kind == IntradayOfferKind.VoluntaryOvertime ? "Voluntary overtime" : "Voluntary time off";
        var changed = await _shiftService.ChangeTimesAsync(shift.ItemId, start, end, ScheduleChangeInitiator.Worker, reason, actor, cancellationToken);

        if (!changed.Succeeded)
        {
            return changed.Errors;
        }

        offer.Claims.Add(new IntradayOfferClaim { WorkerId = worker.ItemId, ShiftEventId = shift.ItemId, ClaimedUtc = _clock.UtcNow });

        if (offer.Claims.Count >= offer.Places)
        {
            offer.Status = IntradayOfferStatus.Filled;
        }

        await _session.SaveAsync(offer, checkConcurrency: true, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

        return [];
    }
}
