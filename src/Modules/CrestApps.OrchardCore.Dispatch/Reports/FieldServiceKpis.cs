using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.Reports;

/// <summary>
/// The standard field service KPIs over a range of days.
/// </summary>
public sealed class FieldServiceKpiResult
{
    /// <summary>
    /// Gets or sets the visits that started in the range and finished, completed or not.
    /// </summary>
    public int FinishedVisits { get; set; }

    /// <summary>
    /// Gets or sets the share of finished visits done the first time, from 0 to 1.
    /// </summary>
    public double? FirstTimeFixRate { get; set; }

    /// <summary>
    /// Gets or sets the share of arrivals inside the window, from 0 to 1.
    /// </summary>
    public double? OnTimeArrivalRate { get; set; }

    /// <summary>
    /// Gets or sets the minutes spent travelling to visits.
    /// </summary>
    public int TravelMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes spent on site.
    /// </summary>
    public int OnSiteMinutes { get; set; }

    /// <summary>
    /// Gets or sets the visits completed per technician on the days they did any.
    /// </summary>
    public double? JobsPerTechnicianDay { get; set; }

    /// <summary>
    /// Gets or sets the share of finished visits that needed a return visit, from 0 to 1.
    /// </summary>
    public double? ReturnVisitRate { get; set; }

    /// <summary>
    /// Gets or sets the work orders made in the range.
    /// </summary>
    public int WorkOrders { get; set; }

    /// <summary>
    /// Gets or sets the share answered by their response time, from 0 to 1.
    /// </summary>
    public double? ResponseCompliance { get; set; }

    /// <summary>
    /// Gets or sets the share completed by their resolution time, from 0 to 1.
    /// </summary>
    public double? ResolutionCompliance { get; set; }

    /// <summary>
    /// Gets or sets the mean hours from making a work order to completing it.
    /// </summary>
    public double? MeanHoursToRepair { get; set; }

    /// <summary>
    /// Gets or sets whether there were more records than are counted, so the numbers cover only the first ones.
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// Works out the field service KPIs from the visits that started and the work orders made in a range.
/// </summary>
public sealed class FieldServiceKpiService
{
    // The most records read for one range, so a long range can't load the whole store.
    internal const int MaxRecords = 5000;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldServiceKpiService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public FieldServiceKpiService(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Works out the KPIs.
    /// </summary>
    /// <param name="fromUtc">The start of the range, in UTC.</param>
    /// <param name="toUtc">The end of the range, in UTC.</param>
    /// <param name="timeZoneId">The time zone the days are counted in.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The KPIs.</returns>
    public async Task<FieldServiceKpiResult> GetAsync(DateTime fromUtc, DateTime toUtc, string timeZoneId, CancellationToken cancellationToken = default)
    {
        var visits = (await _session.Query<ServiceVisit, ServiceVisitIndex>(index => index.StartUtc >= fromUtc && index.StartUtc < toUtc, collection: DispatchConstants.CollectionName)
            .Take(MaxRecords + 1)
            .ListAsync(cancellationToken)).ToList();
        var workOrders = (await _session.Query<WorkOrder, WorkOrderIndex>(index => index.CreatedUtc >= fromUtc && index.CreatedUtc < toUtc, collection: WorkOrdersConstants.CollectionName)
            .Take(MaxRecords + 1)
            .ListAsync(cancellationToken)).ToList();
        var result = new FieldServiceKpiResult { Truncated = visits.Count > MaxRecords || workOrders.Count > MaxRecords };

        visits = [.. visits.Take(MaxRecords)];
        workOrders = [.. workOrders.Take(MaxRecords)];

        var finished = visits.Where(visit => visit.Status is VisitStatus.Completed or VisitStatus.Incomplete).ToList();
        var arrivals = visits.Select(FieldServiceMeasures.OnTime).Where(onTime => onTime.HasValue).ToList();
        var completed = finished.Where(visit => visit.Status == VisitStatus.Completed).ToList();
        var technicianDays = completed
            .Where(visit => visit.StartUtc.HasValue && !string.IsNullOrEmpty(visit.LeadWorkerId))
            .GroupBy(visit => (visit.LeadWorkerId, Day: CalendarTime.ToLocal(visit.StartUtc.Value, timeZoneId).Date))
            .ToList();

        result.FinishedVisits = finished.Count;
        result.FirstTimeFixRate = Rate(finished.Count(visit => visit.Status == VisitStatus.Completed && string.IsNullOrEmpty(visit.ReturnVisitId)), finished.Count);
        result.ReturnVisitRate = Rate(finished.Count(visit => !string.IsNullOrEmpty(visit.ReturnVisitId)), finished.Count);
        result.OnTimeArrivalRate = Rate(arrivals.Count(onTime => onTime == true), arrivals.Count);
        result.TravelMinutes = finished.Sum(visit => visit.TravelMinutes ?? 0);
        result.OnSiteMinutes = finished.Sum(visit => FieldServiceMeasures.OnSiteMinutes(visit) ?? 0);
        result.JobsPerTechnicianDay = technicianDays.Count == 0 ? null : Math.Round(technicianDays.Average(group => group.Count()), 2);

        var responded = workOrders.Select(FieldServiceMeasures.ResponseMet).Where(met => met.HasValue).ToList();
        var resolved = workOrders.Select(FieldServiceMeasures.ResolutionMet).Where(met => met.HasValue).ToList();
        var repaired = workOrders.Where(workOrder => workOrder.CompletedUtc.HasValue).ToList();

        result.WorkOrders = workOrders.Count;
        result.ResponseCompliance = Rate(responded.Count(met => met == true), responded.Count);
        result.ResolutionCompliance = Rate(resolved.Count(met => met == true), resolved.Count);
        result.MeanHoursToRepair = repaired.Count == 0 ? null : Math.Round(repaired.Average(workOrder => (workOrder.CompletedUtc.Value - workOrder.CreatedUtc).TotalHours), 1);

        return result;
    }

    private static double? Rate(int part, int whole) => whole == 0 ? null : (double)part / whole;
}
