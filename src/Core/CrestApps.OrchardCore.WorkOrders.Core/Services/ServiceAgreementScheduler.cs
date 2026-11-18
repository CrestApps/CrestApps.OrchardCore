using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Calendars.Recurrence;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NodaTime;
using OrchardCore.Modules;
using YesSql;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Makes the work orders of the visits of active agreements ahead of time, once each. The agreements are kept by the
/// Agreements module, which also expires them and tells when one is about to end; the visits are the agreement's Work
/// Orders terms.
/// </summary>
public sealed class ServiceAgreementScheduler
{
    /// <summary>
    /// Who the work orders it makes are created by.
    /// </summary>
    public const string Actor = "Service agreement";

    private readonly ISession _session;
    private readonly IAgreementManager _agreements;
    private readonly WorkOrderService _workOrders;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementScheduler"/> class.
    /// </summary>
    public ServiceAgreementScheduler(
        ISession session,
        IAgreementManager agreements,
        WorkOrderService workOrders,
        IClock clock,
        ILogger<ServiceAgreementScheduler> logger,
        IStringLocalizer<ServiceAgreementScheduler> stringLocalizer)
    {
        _session = session;
        _agreements = agreements;
        _workOrders = workOrders;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Runs every active agreement with visits.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many work orders were made.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var active = nameof(AgreementStatus.Active);
        var ids = (await _session.QueryIndex<AgreementIndex>(index => index.Status == active, collection: AgreementsConstants.CollectionName).ListAsync(cancellationToken))
            .Select(index => index.ItemId)
            .ToList();
        var made = 0;

        foreach (var id in ids)
        {
            var agreement = await _agreements.FindByIdAsync(id, cancellationToken);

            if (agreement is null)
            {
                continue;
            }

            try
            {
                made += (await RunAsync(agreement, cancellationToken)).Count;
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                _logger.LogError(ex, "The agreement {AgreementId} couldn't make its work orders.", id);
            }
        }

        return made;
    }

    /// <summary>
    /// Runs one agreement: makes the work orders of its visits due within each schedule's lead time that don't exist
    /// yet, while it is in force.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The work orders made.</returns>
    public async Task<IReadOnlyList<WorkOrder>> RunAsync(Agreement agreement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        var made = new List<WorkOrder>();
        var zone = ZoneOf(agreement.TimeZoneId);
        var today = Instant.FromDateTimeUtc(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).InZone(zone).Date;

        if (!agreement.IsInForceOn(today.ToDateOnly())
            || !agreement.TryGet<WorkOrderAgreementTerms>(out var terms)
            || terms?.Schedules is not { Count: > 0 })
        {
            return made;
        }

        var source = nameof(WorkOrderSource.Agreement);

        foreach (var schedule in terms.Schedules)
        {
            foreach (var date in Occurrences(agreement, schedule, zone, today, today.PlusDays(Math.Max(0, schedule.LeadDays))))
            {
                var key = SourceKey(agreement.ItemId, schedule.Id, date);

                if (await _session.QueryIndex<WorkOrderIndex>(index => index.Source == source && index.SourceId == key, collection: WorkOrdersConstants.CollectionName).FirstOrDefaultAsync(cancellationToken) is not null)
                {
                    continue;
                }

                var workOrder = await _workOrders.NewAsync(schedule.TypeId, cancellationToken);

                if (workOrder is null)
                {
                    _logger.LogWarning("The agreement {AgreementId} has a schedule whose work order type {TypeId} doesn't exist.", agreement.ItemId, schedule.TypeId);

                    break;
                }

                var (startUtc, endUtc) = DayOf(date, zone);

                workOrder.Summary = S["{0} ({1})", string.IsNullOrWhiteSpace(schedule.Name) ? agreement.Name : schedule.Name, agreement.Number];
                workOrder.CustomerId = terms.CustomerId;
                workOrder.AccountId = agreement.AccountId;
                workOrder.Contact = new WorkOrderContact { Name = terms.Contact?.Name, Phone = terms.Contact?.Phone, Email = terms.Contact?.Email };
                workOrder.ServiceAddress = terms.ServiceAddress;
                workOrder.AccessNotes = terms.AccessNotes;
                workOrder.AssetIds = [.. schedule.AssetIds.Count > 0 ? schedule.AssetIds : terms.AssetIds];
                workOrder.EarliestUtc = startUtc;
                workOrder.LatestUtc = endUtc;
                workOrder.Source = WorkOrderSource.Agreement;
                workOrder.SourceId = key;
                workOrder.AgreementId = agreement.ItemId;
                workOrder.AgreedPrice = terms.Pricing == AgreementPricing.Included ? 0m : terms.PricePerVisit;

                var result = await _workOrders.CreateAsync(workOrder, Actor, cancellationToken);

                if (result.Succeeded)
                {
                    made.Add(workOrder);
                }
                else
                {
                    _logger.LogWarning("The agreement {AgreementId} couldn't make the work order of {Date}: {Errors}", agreement.ItemId, date.ToString("R", CultureInfo.InvariantCulture), string.Join(" ", result.Errors));
                }
            }
        }

        return made;
    }

    /// <summary>
    /// Gives the days a schedule has a visit on, between two days, in the agreement's period. Pure.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="schedule">The schedule.</param>
    /// <param name="zone">The agreement's time zone.</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <returns>The days, in order.</returns>
    public static IEnumerable<LocalDate> Occurrences(Agreement agreement, AgreementSchedule schedule, DateTimeZone zone, LocalDate from, LocalDate to)
    {
        ArgumentNullException.ThrowIfNull(agreement);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(zone);

        if (!RecurrenceRule.TryParse(schedule.Rule, out var rule, out _))
        {
            yield break;
        }

        var last = agreement.EndDate is { } end && LocalDate.FromDateOnly(end) < to ? LocalDate.FromDateOnly(end) : to;

        foreach (var occurrence in RecurrenceExpander.GetOccurrences(rule, LocalDate.FromDateOnly(agreement.StartDate).AtMidnight(), zone))
        {
            if (occurrence.Date > last)
            {
                yield break;
            }

            if (occurrence.Date >= from)
            {
                yield return occurrence.Date;
            }
        }
    }

    /// <summary>
    /// Gives the start and end of a day in a time zone, in UTC: the window of an agreement's visit. Pure.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns>When the day starts and the next one starts.</returns>
    public static (DateTime StartUtc, DateTime EndUtc) DayOf(LocalDate date, DateTimeZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return (zone.AtStartOfDay(date).ToDateTimeUtc(), zone.AtStartOfDay(date.PlusDays(1)).ToDateTimeUtc());
    }

    /// <summary>
    /// Gives the source id of a visit's work order, which makes it once.
    /// </summary>
    /// <param name="agreementId">The agreement.</param>
    /// <param name="scheduleId">The schedule.</param>
    /// <param name="date">The day.</param>
    /// <returns>The source id.</returns>
    public static string SourceKey(string agreementId, string scheduleId, LocalDate date)
        => string.Create(CultureInfo.InvariantCulture, $"{agreementId}:{scheduleId}:{date.Year:D4}{date.Month:D2}{date.Day:D2}");

    /// <summary>
    /// Gives a time zone by id, or UTC when it isn't known.
    /// </summary>
    /// <param name="timeZoneId">The time zone.</param>
    /// <returns>The time zone.</returns>
    public static DateTimeZone ZoneOf(string timeZoneId)
        => AgreementPeriods.ZoneOf(timeZoneId);
}
