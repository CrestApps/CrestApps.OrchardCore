using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Geo.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// Keeps technicians' positions only when they agreed to the current notice and are working: on the way to, at,
/// or between visits of the day. A position on the way to a visit refreshes its ETA.
/// </summary>
public sealed class WorkerLocationService : IWorkerLocationService
{
    private const int MaxPings = 500;
    private const int PruneBatch = 1000;

    private static readonly TimeSpan _workingWindow = TimeSpan.FromHours(12);

    private readonly WorkerLocationConsentStore _consents;
    private readonly WorkerLocationPingStore _pings;
    private readonly FieldCommandReceiptStore _receipts;
    private readonly ServiceVisitStore _visits;
    private readonly IDispatchService _dispatchService;
    private readonly DispatchResources _resources;
    private readonly DispatchMobileOptions _options;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerLocationService"/> class.
    /// </summary>
    /// <param name="consents">The consent store.</param>
    /// <param name="pings">The position store.</param>
    /// <param name="receipts">The records of commands.</param>
    /// <param name="visits">The visit store.</param>
    /// <param name="dispatchService">The dispatch service.</param>
    /// <param name="resources">The dispatch resources, for travel times.</param>
    /// <param name="options">The technician app's settings.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkerLocationService(
        WorkerLocationConsentStore consents,
        WorkerLocationPingStore pings,
        FieldCommandReceiptStore receipts,
        ServiceVisitStore visits,
        IDispatchService dispatchService,
        DispatchResources resources,
        IOptions<DispatchMobileOptions> options,
        IClock clock,
        IStringLocalizer<WorkerLocationService> stringLocalizer)
    {
        _consents = consents;
        _pings = pings;
        _receipts = receipts;
        _visits = visits;
        _dispatchService = dispatchService;
        _resources = resources;
        _options = options.Value;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<bool> HasConsentAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var consent = string.IsNullOrEmpty(workerId) ? null : await _consents.FindByWorkerAsync(workerId, cancellationToken);

        return consent?.NoticeVersion is not null && consent.NoticeVersion == _options.ConsentNoticeVersion;
    }

    /// <inheritdoc/>
    public async Task<WorkerLocationConsent> SetConsentAsync(string workerId, bool given, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);

        var now = _clock.UtcNow;
        var consent = await _consents.FindByWorkerAsync(workerId, cancellationToken);
        var isNew = consent is null;

        consent ??= new WorkerLocationConsent { WorkerId = workerId };
        consent.NoticeVersion = given ? _options.ConsentNoticeVersion : null;
        consent.Utc = now;
        consent.History.Add(new WorkerLocationConsentChange { Utc = now, Given = given, NoticeVersion = _options.ConsentNoticeVersion });

        if (isNew)
        {
            await _consents.CreateAsync(consent, cancellationToken);
        }
        else
        {
            await _consents.UpdateAsync(consent, cancellationToken);
        }

        // Withdrawing takes the positions kept so far with it.
        if (!given)
        {
            await _pings.PruneAsync(now, workerId, PruneBatch * 10, cancellationToken);
        }

        return consent;
    }

    /// <inheritdoc/>
    public async Task<LocationPingResult> RecordAsync(string workerId, IReadOnlyList<LocationPingInput> pings, CancellationToken cancellationToken = default)
    {
        if (pings is null || pings.Count == 0)
        {
            return new LocationPingResult();
        }

        if (!await HasConsentAsync(workerId, cancellationToken))
        {
            return new LocationPingResult { Reason = S["Location sharing is off."] };
        }

        var now = _clock.UtcNow;
        var visits = await _dispatchService.ListScheduledAsync(now - _workingWindow, now + _workingWindow, [workerId], cancellationToken);
        var active = visits.FirstOrDefault(visit => visit.WorkerIds.Contains(workerId) && visit.Status is VisitStatus.EnRoute or VisitStatus.OnSite or VisitStatus.Paused);
        var next = active ?? visits
            .Where(visit => visit.WorkerIds.Contains(workerId) && visit.Status is VisitStatus.Scheduled or VisitStatus.Dispatched && visit.EndUtc > now)
            .OrderBy(visit => visit.StartUtc)
            .FirstOrDefault();

        // Positions are kept only while working, never off shift.
        if (next is null)
        {
            return new LocationPingResult { Reason = S["You have no visit now, so your location isn't kept."] };
        }

        var kept = pings
            .Where(ping => ping is not null && ping.Utc > now.AddDays(-1) && ping.Utc <= now.AddMinutes(5) && Math.Abs(ping.Latitude) <= 90 && Math.Abs(ping.Longitude) <= 180)
            .OrderBy(ping => ping.Utc)
            .Take(MaxPings)
            .ToList();

        foreach (var ping in kept)
        {
            await _pings.CreateAsync(new WorkerLocationPing
            {
                WorkerId = workerId,
                Utc = ping.Utc,
                Latitude = ping.Latitude,
                Longitude = ping.Longitude,
                AccuracyMeters = ping.AccuracyMeters,
                Speed = ping.Speed,
                Heading = ping.Heading,
                VisitId = next.ItemId,
            }, cancellationToken);
        }

        if (kept.Count > 0 && next.Status == VisitStatus.EnRoute)
        {
            await RefreshEtaAsync(next, kept[^1], cancellationToken);
        }

        return new LocationPingResult { Kept = kept.Count };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkerLocationPing>> LatestAsync(IReadOnlyCollection<string> workerIds, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var rows = await _pings.ListSinceAsync(workerIds ?? [], sinceUtc, cancellationToken);

        return [.. rows
            .GroupBy(row => row.WorkerId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(row => new WorkerLocationPing { ItemId = row.ItemId, WorkerId = row.WorkerId, Utc = row.Utc, Latitude = row.Latitude, Longitude = row.Longitude, VisitId = row.VisitId })];
    }

    /// <inheritdoc/>
    public async Task<int> PruneAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var pings = await _pings.PruneAsync(now.AddDays(-Math.Max(1, _options.LocationRetentionDays)), null, PruneBatch, cancellationToken);
        var receipts = await _receipts.PruneAsync(now.AddDays(-Math.Max(1, _options.ReceiptRetentionDays)), PruneBatch, cancellationToken);

        return pings + receipts;
    }

    private async Task RefreshEtaAsync(ServiceVisit visit, LocationPingInput ping, CancellationToken cancellationToken)
    {
        var here = await _resources.PlaceOfAsync(visit.WorkOrderId, cancellationToken);
        var trip = await _resources.TravelAsync(new GeoPoint(ping.Latitude, ping.Longitude), here, ping.Utc, cancellationToken);

        if (trip is null)
        {
            return;
        }

        var eta = ping.Utc + trip.Value.Duration;

        // A minute either way isn't worth a write.
        if (visit.EtaUtc is { } current && Math.Abs((eta - current).TotalMinutes) < 1)
        {
            return;
        }

        visit.EtaUtc = eta;
        visit.ModifiedUtc = _clock.UtcNow;
        await _visits.UpdateAsync(visit, cancellationToken);
    }
}
