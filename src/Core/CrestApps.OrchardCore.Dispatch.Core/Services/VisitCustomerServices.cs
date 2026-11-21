using System.Globalization;
using System.Security.Cryptography;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Core.Telemetry;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using YesSql;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// What notices say about the business and where tracking links point; the module gives the site's.
/// </summary>
public interface IDispatchSiteInfo
{
    /// <summary>
    /// Gets the business's name, for notices.
    /// </summary>
    /// <returns>The name.</returns>
    Task<string> CompanyNameAsync();

    /// <summary>
    /// Gives the full address of a tracking link.
    /// </summary>
    /// <param name="token">The link's token.</param>
    /// <returns>The address, or <see langword="null"/> when it can't be made.</returns>
    Task<string> TrackingUrlAsync(string token);
}

/// <summary>
/// Signs tracking links with <see cref="SignedTokenService"/>. A link works until a week after it was made and stops
/// when the visit is done, canceled or ended incomplete. The purpose is the one the links always had, so links already
/// sent keep working.
/// </summary>
public sealed class VisitTrackingLinks : IVisitTrackingLinks
{
    internal const string Purpose = "CrestApps.Dispatch.Tracking";

    private static readonly TimeSpan _lifetime = TimeSpan.FromDays(7);

    private readonly SignedTokenService _tokens;
    private readonly ServiceVisitStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisitTrackingLinks"/> class.
    /// </summary>
    public VisitTrackingLinks(SignedTokenService tokens, ServiceVisitStore store)
    {
        _tokens = tokens;
        _store = store;
    }

    /// <inheritdoc/>
    public string CreateToken(ServiceVisit visit)
    {
        ArgumentNullException.ThrowIfNull(visit);

        return _tokens.Protect(Purpose, visit.ItemId, _lifetime);
    }

    /// <inheritdoc/>
    public async Task<ServiceVisit> ReadAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var visitId = _tokens.Unprotect(Purpose, token);

        if (visitId is null)
        {
            return null;
        }

        var visit = await _store.FindByIdAsync(visitId, cancellationToken);

        return visit is null || !VisitStatusRules.IsOpen(visit.Status) ? null : visit;
    }
}

/// <summary>
/// Tells customers about their visit: when it is scheduled or moved, the day before, when the technician is on the
/// way (with a tracking link), and when it is done or canceled. Each notice is sent once for each start.
/// </summary>
public sealed class VisitNoticeService : IVisitObserver
{
    private readonly ServiceVisitStore _store;
    private readonly WorkOrderStore _workOrders;
    private readonly DispatchResources _resources;
    private readonly IEnumerable<IVisitNoticeSender> _senders;
    private readonly IVisitTrackingLinks _links;
    private readonly IEnumerable<IDispatchSiteInfo> _siteInfo;
    private readonly IDispatchSettingsProvider _settingsProvider;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisitNoticeService"/> class.
    /// </summary>
    public VisitNoticeService(
        ServiceVisitStore store,
        WorkOrderStore workOrders,
        DispatchResources resources,
        IEnumerable<IVisitNoticeSender> senders,
        IVisitTrackingLinks links,
        IEnumerable<IDispatchSiteInfo> siteInfo,
        IDispatchSettingsProvider settingsProvider,
        ISession session,
        IClock clock,
        ILogger<VisitNoticeService> logger,
        IStringLocalizer<VisitNoticeService> stringLocalizer)
    {
        _store = store;
        _workOrders = workOrders;
        _resources = resources;
        _senders = senders;
        _links = links;
        _siteInfo = siteInfo;
        _settingsProvider = settingsProvider;
        _session = session;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task OnEventAsync(ServiceVisit visit, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visit);

        var kind = eventName switch
        {
            DispatchConstants.Events.Assigned => DispatchConstants.Notices.Scheduled,
            DispatchConstants.Events.StatusChanged when visit.Status == VisitStatus.EnRoute => DispatchConstants.Notices.EnRoute,
            DispatchConstants.Events.StatusChanged when visit.Status == VisitStatus.Completed => DispatchConstants.Notices.Completed,
            DispatchConstants.Events.StatusChanged when visit.Status == VisitStatus.Canceled => DispatchConstants.Notices.Canceled,
            _ => null,
        };

        if (kind is not null)
        {
            await SendAsync(visit.ItemId, kind, cancellationToken);
        }
    }

    /// <summary>
    /// Sends the reminders of tomorrow's visits once it is the reminder hour.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were sent.</returns>
    public async Task<int> SendRemindersAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsProvider.GetAsync();

        if (!settings.NoticesEnabled)
        {
            return 0;
        }

        var zone = await ZoneAsync();
        var now = _clock.UtcNow;
        var local = CalendarTime.ToLocal(now, zone);

        if (local.Hour < settings.ReminderHour)
        {
            return 0;
        }

        var tomorrow = DateOnly.FromDateTime(local).AddDays(1);
        var fromUtc = CalendarTime.ToUtc(tomorrow.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(tomorrow.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var scheduled = nameof(VisitStatus.Scheduled);
        var dispatched = nameof(VisitStatus.Dispatched);
        var ids = (await _session.QueryIndex<ServiceVisitIndex>(index => (index.Status == scheduled || index.Status == dispatched) && index.StartUtc >= fromUtc && index.StartUtc < toUtc, collection: DispatchConstants.CollectionName).ListAsync(cancellationToken))
            .Select(index => index.ItemId)
            .ToList();
        var sent = 0;

        foreach (var id in ids)
        {
            if (await SendAsync(id, DispatchConstants.Notices.Reminder, cancellationToken))
            {
                sent++;
            }
        }

        return sent;
    }

    // Reloads the visit: an assignment saves before telling observers, which leaves what it read behind.
    private async Task<bool> SendAsync(string visitId, string kind, CancellationToken cancellationToken)
    {
        var settings = await _settingsProvider.GetAsync();

        if (!settings.NoticesEnabled || !_senders.Any())
        {
            return false;
        }

        var visit = await _store.FindByIdAsync(visitId, cancellationToken);

        if (visit is null || visit.Notices.Any(notice => notice.Kind == kind && notice.StartUtc == visit.StartUtc && notice.Error is null))
        {
            return false;
        }

        var workOrder = await _workOrders.FindByIdAsync(visit.WorkOrderId, cancellationToken);
        var message = new VisitNoticeMessage
        {
            Visit = visit,
            Kind = kind,
            Phone = workOrder?.Contact?.Phone,
            Email = workOrder?.Contact?.Email,
        };
        var sender = _senders.FirstOrDefault(item => item.CanSend(message));

        if (sender is null)
        {
            return false;
        }

        var company = await (_siteInfo.FirstOrDefault()?.CompanyNameAsync() ?? Task.FromResult<string>(null)) ?? string.Empty;

        message.Subject = S["{0}: your visit {1}", company, visit.Number];
        message.Text = await FillAsync(Template(settings, kind), visit, company, settings, cancellationToken);

        var error = await sender.SendAsync(message, cancellationToken);

        visit.Notices.Add(new VisitNotice { Kind = kind, Utc = _clock.UtcNow, Channel = sender.Channel, StartUtc = visit.StartUtc, Error = error });
        await _store.UpdateAsync(visit, cancellationToken);

        if (error is not null)
        {
            _logger.LogWarning("The {Kind} notice of visit {VisitNumber} wasn't sent: {Error}", kind, visit.Number, error);
        }

        return error is null;
    }

    private async Task<string> FillAsync(string template, ServiceVisit visit, string company, DispatchSettings settings, CancellationToken cancellationToken)
    {
        var zone = await ZoneAsync();
        var lead = await _resources.FindWorkerAsync(visit.LeadWorkerId, cancellationToken);
        var trackingUrl = settings.TrackingEnabled && _siteInfo.FirstOrDefault() is { } site ? await site.TrackingUrlAsync(_links.CreateToken(visit)) : null;
        string Time(DateTime? utc) => utc is null ? string.Empty : CalendarTime.ToLocal(utc.Value, zone).ToString("HH:mm", CultureInfo.CurrentCulture);

        return (template ?? string.Empty)
            .Replace("{Company}", company, StringComparison.Ordinal)
            .Replace("{Number}", visit.Number, StringComparison.Ordinal)
            .Replace("{Date}", visit.StartUtc is null ? string.Empty : CalendarTime.ToLocal(visit.StartUtc.Value, zone).ToString("dddd d MMMM", CultureInfo.CurrentCulture), StringComparison.Ordinal)
            .Replace("{Window}", S["{0} and {1}", Time(visit.ArrivalStartUtc ?? visit.StartUtc), Time(visit.ArrivalEndUtc ?? visit.StartUtc)].Value, StringComparison.Ordinal)
            .Replace("{Technician}", FirstName(lead?.DisplayName), StringComparison.Ordinal)
            .Replace("{Eta}", Time(visit.EtaUtc ?? visit.StartUtc), StringComparison.Ordinal)
            .Replace("{TrackingUrl}", trackingUrl ?? string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private async Task<string> ZoneAsync()
    {
        var zone = await _settingsProvider.GetTimeZoneIdAsync();

        return CalendarTime.IsValidZone(zone) ? zone : "UTC";
    }

    private static string Template(DispatchSettings settings, string kind) => kind switch
    {
        DispatchConstants.Notices.Scheduled => settings.ScheduledText,
        DispatchConstants.Notices.Reminder => settings.ReminderText,
        DispatchConstants.Notices.EnRoute => settings.EnRouteText,
        DispatchConstants.Notices.Completed => settings.CompletedText,
        _ => settings.CanceledText,
    };

    /// <summary>
    /// Gives the first word of a name, what customers see of their technician.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The first name.</returns>
    public static string FirstName(string name)
        => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim().Split(' ', 2)[0];
}

/// <summary>
/// Flags the visits at risk today: running over, not started, arriving after the window, or with a technician on time
/// off; and clears the flag once it no longer holds. The At Risk event starts once for each new reason.
/// </summary>
public sealed class VisitRiskMonitor
{
    private readonly ISession _session;
    private readonly ServiceVisitStore _store;
    private readonly DispatchResources _resources;
    private readonly IDispatchSettingsProvider _settingsProvider;
    private readonly IEnumerable<IVisitObserver> _observers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisitRiskMonitor"/> class.
    /// </summary>
    public VisitRiskMonitor(
        ISession session,
        ServiceVisitStore store,
        DispatchResources resources,
        IDispatchSettingsProvider settingsProvider,
        IEnumerable<IVisitObserver> observers,
        IClock clock,
        ILogger<VisitRiskMonitor> logger,
        IStringLocalizer<VisitRiskMonitor> stringLocalizer)
    {
        _session = session;
        _store = store;
        _resources = resources;
        _settingsProvider = settingsProvider;
        _observers = observers;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Checks today's open visits.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many are at risk.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsProvider.GetAsync();
        var zone = await _settingsProvider.GetTimeZoneIdAsync();

        zone = CalendarTime.IsValidZone(zone) ? zone : "UTC";

        var now = _clock.UtcNow;
        var grace = TimeSpan.FromMinutes(Math.Max(0, settings.AtRiskMinutes));
        var from = now.AddDays(-1);
        var to = now.AddHours(12);
        var ids = (await _session.QueryIndex<ServiceVisitIndex>(index => index.IsOpen && index.StartUtc != null && index.StartUtc < to && index.EndUtc > from, collection: DispatchConstants.CollectionName).ListAsync(cancellationToken))
            .Select(index => index.ItemId)
            .ToList();
        var atRisk = 0;

        foreach (var id in ids)
        {
            var visit = await _store.FindByIdAsync(id, cancellationToken);

            if (visit is null)
            {
                continue;
            }

            var reason = await ReasonAsync(visit, now, grace, zone, cancellationToken);

            if (reason is not null)
            {
                atRisk++;
            }

            if (reason == visit.AtRiskReason)
            {
                continue;
            }

            visit.AtRiskReason = reason;
            visit.AtRiskSinceUtc = reason is null ? null : now;
            await _store.UpdateAsync(visit, cancellationToken);

            if (reason is null)
            {
                continue;
            }

            DispatchTelemetry.RecordVisitEvent(DispatchConstants.Events.AtRisk, visit.Status);

            foreach (var observer in _observers)
            {
                try
                {
                    await observer.OnEventAsync(visit, DispatchConstants.Events.AtRisk, new Dictionary<string, object> { ["Reason"] = reason }, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    DispatchTelemetry.RecordObserverFailure(observer.GetType(), DispatchConstants.Events.AtRisk);
                    _logger.LogError(exception, "A visit observer failed on the at-risk event of visit {VisitNumber}.", visit.Number);
                }
            }
        }

        return atRisk;
    }

    private async Task<string> ReasonAsync(ServiceVisit visit, DateTime now, TimeSpan grace, string zone, CancellationToken cancellationToken)
    {
        string Time(DateTime? utc) => utc is null ? string.Empty : CalendarTime.ToLocal(utc.Value, zone).ToString("HH:mm", CultureInfo.CurrentCulture);

        if (visit.Status is VisitStatus.OnSite or VisitStatus.Paused && visit.EndUtc + grace < now)
        {
            return S["Running over: it was to end at {0}.", Time(visit.EndUtc)];
        }

        if (visit.Status is VisitStatus.Scheduled or VisitStatus.Dispatched && visit.StartUtc + grace < now)
        {
            return S["Not started: it was to start at {0}.", Time(visit.StartUtc)];
        }

        if (visit.Status == VisitStatus.EnRoute && visit.EtaUtc is { } eta && visit.ArrivalEndUtc is { } windowEnd && eta > windowEnd)
        {
            return S["Arriving at {0}, after the window that ends at {1}.", Time(eta), Time(windowEnd)];
        }

        if (visit.Status is VisitStatus.Scheduled or VisitStatus.Dispatched)
        {
            foreach (var workerId in visit.WorkerIds)
            {
                var worker = await _resources.FindWorkerAsync(workerId, cancellationToken);

                if (worker is null)
                {
                    continue;
                }

                var availability = await _resources.GetAvailabilityAsync(worker, visit.StartUtc.Value, visit.EndUtc.Value, visit.ItemId, cancellationToken);

                if (availability.TimeOff.Any(span => span.Overlaps(new TimeWindow(visit.StartUtc.Value, visit.EndUtc.Value))))
                {
                    return S["{0} is on time off then.", worker.DisplayName];
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Passes visit events to the notice service. Kept apart so the notice service can be asked for reminders on its own.
/// </summary>
internal sealed class VisitNoticeObserver : IVisitObserver
{
    private readonly VisitNoticeService _notices;

    public VisitNoticeObserver(VisitNoticeService notices)
    {
        _notices = notices;
    }

    public Task OnEventAsync(ServiceVisit visit, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default)
        => _notices.OnEventAsync(visit, eventName, detail, cancellationToken);
}
