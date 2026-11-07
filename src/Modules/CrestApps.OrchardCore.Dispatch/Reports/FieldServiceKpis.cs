using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Services;

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
    /// Gets or sets what the completed work orders billed on average, in <see cref="RevenueCurrency"/>.
    /// </summary>
    public decimal? RevenuePerWorkOrder { get; set; }

    /// <summary>
    /// Gets or sets the currency of <see cref="RevenuePerWorkOrder"/>: the one most completed work orders bill in.
    /// </summary>
    public string RevenueCurrency { get; set; }

    /// <summary>
    /// Gets or sets the parts the completed work orders used, by the stock location they came from, most first.
    /// </summary>
    public IReadOnlyList<FieldServicePartsUsed> PartsByLocation { get; set; } = [];

    /// <summary>
    /// Gets or sets the days within which another work order on the same asset counts as repeat work.
    /// </summary>
    public int RepeatDays { get; set; }

    /// <summary>
    /// Gets or sets the share of completed work orders with assets that had another work order on one of them
    /// within <see cref="RepeatDays"/>, from 0 to 1.
    /// </summary>
    public double? RepeatWorkRate { get; set; }

    /// <summary>
    /// Gets or sets whether there were more records than are counted, so the numbers cover only the first ones.
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// The parts used from one stock location, such as a van.
/// </summary>
public sealed class FieldServicePartsUsed
{
    /// <summary>
    /// Gets or sets the stock location.
    /// </summary>
    public string LocationId { get; set; }

    /// <summary>
    /// Gets or sets its name, or its id when it isn't known.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the units used.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets how many work orders used them.
    /// </summary>
    public int WorkOrders { get; set; }
}

/// <summary>
/// Works out the field service KPIs from the visits that started and the work orders made in a range.
/// </summary>
public sealed class FieldServiceKpiService
{
    // The most records read for one range, so a long range can't load the whole store.
    internal const int MaxRecords = 5000;

    /// <summary>
    /// The days within which another work order on the same asset counts as repeat work, unless told otherwise.
    /// </summary>
    public const int DefaultRepeatDays = 30;

    private readonly ISession _session;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldServiceKpiService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="types">The work order types, for what each work order bills.</param>
    /// <param name="serviceProvider">The services, for the stock locations' names when Inventory is on.</param>
    public FieldServiceKpiService(ISession session, WorkOrderSettingManager<WorkOrderType> types, IServiceProvider serviceProvider)
    {
        _session = session;
        _types = types;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Works out the KPIs.
    /// </summary>
    /// <param name="fromUtc">The start of the range, in UTC.</param>
    /// <param name="toUtc">The end of the range, in UTC.</param>
    /// <param name="timeZoneId">The time zone the days are counted in.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The KPIs.</returns>
    public Task<FieldServiceKpiResult> GetAsync(DateTime fromUtc, DateTime toUtc, string timeZoneId, CancellationToken cancellationToken = default)
        => GetAsync(fromUtc, toUtc, timeZoneId, DefaultRepeatDays, cancellationToken);

    /// <summary>
    /// Works out the KPIs, counting repeat work within the given days.
    /// </summary>
    /// <param name="fromUtc">The start of the range, in UTC.</param>
    /// <param name="toUtc">The end of the range, in UTC.</param>
    /// <param name="timeZoneId">The time zone the days are counted in.</param>
    /// <param name="repeatDays">The days within which another work order on the same asset counts as repeat work.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The KPIs.</returns>
    public async Task<FieldServiceKpiResult> GetAsync(DateTime fromUtc, DateTime toUtc, string timeZoneId, int repeatDays, CancellationToken cancellationToken = default)
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

        await CountRevenueAsync(result, repaired, cancellationToken);
        await CountPartsAsync(result, repaired);
        await CountRepeatWorkAsync(result, repaired, Math.Clamp(repeatDays, 1, 365), cancellationToken);

        return result;
    }

    private async Task CountRevenueAsync(FieldServiceKpiResult result, List<WorkOrder> completed, CancellationToken cancellationToken)
    {
        var types = (await _types.GetAllAsync(cancellationToken)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var billed = completed
            .Select(workOrder => (workOrder, Type: types.GetValueOrDefault(workOrder.TypeId ?? string.Empty)))
            .Where(item => item.Type is not null && (item.Type.BillingMode != WorkOrderBillingMode.NotBillable || item.workOrder.AgreedPrice is not null))
            .GroupBy(item => item.Type.Currency ?? "USD", StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        if (billed is null)
        {
            return;
        }

        result.RevenueCurrency = billed.Key.ToUpperInvariant();
        result.RevenuePerWorkOrder = Math.Round(billed.Average(item => WorkOrderService.BillableAmount(item.workOrder, item.Type)), 2);
    }

    private async Task CountPartsAsync(FieldServiceKpiResult result, List<WorkOrder> completed)
    {
        var used = completed
            .SelectMany(workOrder => workOrder.Lines.Where(line => line.Kind == WorkOrderLineKind.Product && line.QuantityUsed > 0 && !string.IsNullOrEmpty(line.LocationId)).Select(line => (workOrder.ItemId, line.LocationId, line.QuantityUsed)))
            .GroupBy(item => item.LocationId, StringComparer.Ordinal)
            .ToList();

        if (used.Count == 0)
        {
            return;
        }

        var names = _serviceProvider.GetService<IInventoryLocationService>() is { } locations
            ? (await locations.GetLocationsAsync()).GroupBy(location => location.LocationId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        result.PartsByLocation = [.. used
            .Select(group => new FieldServicePartsUsed
            {
                LocationId = group.Key,
                Name = names.GetValueOrDefault(group.Key) ?? group.Key,
                Quantity = group.Sum(item => item.QuantityUsed),
                WorkOrders = group.Select(item => item.ItemId).Distinct(StringComparer.Ordinal).Count(),
            })
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture)];
    }

    // A completed work order is repeat work's start when another work order on one of its assets was made after it
    // was completed and within the days.
    private async Task CountRepeatWorkAsync(FieldServiceKpiResult result, List<WorkOrder> completed, int days, CancellationToken cancellationToken)
    {
        result.RepeatDays = days;

        var withAssets = completed.Where(workOrder => workOrder.AssetIds.Count > 0).ToList();

        if (withAssets.Count == 0)
        {
            return;
        }

        var assetIds = withAssets.SelectMany(workOrder => workOrder.AssetIds).Distinct(StringComparer.Ordinal).ToArray();
        var rows = new List<WorkOrderAssetIndex>();

        foreach (var batch in assetIds.Chunk(500))
        {
            rows.AddRange(await _session.QueryIndex<WorkOrderAssetIndex>(index => index.AssetId.IsIn(batch), collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken));
        }

        var otherIds = rows.Select(row => row.WorkOrderId).Distinct(StringComparer.Ordinal).ToArray();
        var made = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        foreach (var batch in otherIds.Chunk(500))
        {
            foreach (var index in await _session.QueryIndex<WorkOrderIndex>(index => index.ItemId.IsIn(batch), collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken))
            {
                made[index.ItemId] = index.CreatedUtc;
            }
        }

        var byAsset = rows.GroupBy(row => row.AssetId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Select(row => row.WorkOrderId).ToList(), StringComparer.Ordinal);
        var repeated = withAssets.Count(workOrder => workOrder.AssetIds.Any(assetId => byAsset.GetValueOrDefault(assetId, []).Any(otherId =>
            otherId != workOrder.ItemId
            && made.TryGetValue(otherId, out var createdUtc)
            && createdUtc > workOrder.CompletedUtc.Value
            && createdUtc <= workOrder.CompletedUtc.Value.AddDays(days))));

        result.RepeatWorkRate = Rate(repeated, withAssets.Count);
    }

    private static double? Rate(int part, int whole) => whole == 0 ? null : (double)part / whole;
}
