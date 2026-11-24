using YesSql;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.Core;
using System.Linq.Expressions;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Dispatch.Reports;

/// <summary>
/// A data set of the field service report data source.
/// </summary>
public interface IFieldServiceReportDataSet : IRecordDataSet
{
}

/// <summary>
/// The Field Service report data source: work orders with their service levels, and visits with their travel,
/// arrival and time on site.
/// </summary>
public sealed class FieldServiceReportDataSource : RecordDataSource, IDataSource
{
    private readonly IFieldServiceReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldServiceReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public FieldServiceReportDataSource(IEnumerable<IFieldServiceReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the data source's technical name.
    /// </summary>
    public static string Name => "FieldService";

    /// <summary>
    /// Gets the data source's name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<FieldServiceReportDataSource>("Field Service");

    /// <summary>
    /// Gets what the data source holds.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<FieldServiceReportDataSource>("Work orders with their service levels, and visits with their travel, arrival and time on site.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// One row per work order: its type, status, priority, territory and how its service level went.
/// </summary>
public sealed class WorkOrdersReportDataSet : IndexedReportDataSet<WorkOrder, WorkOrderIndex>, IFieldServiceReportDataSet
{
    /// <summary>
    /// The data set's name.
    /// </summary>
    public const string DataSetName = "FieldServiceWorkOrders";

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrdersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrdersReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<WorkOrdersReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Work orders"], stringLocalizer["One row per work order: type, status, priority, territory, when it was made, answered and completed, and whether its service level was met."]), session, authorizationService, WorkOrdersConstants.CollectionName, WorkOrdersPermissions.ViewWorkOrders)
    {
        var S = stringLocalizer;
        var group = S["Work order"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Number", S["Number"], DataFieldType.Text, record => record.Number, group);
        AddField("TypeId", S["Type ID"], DataFieldType.Text, record => record.TypeId, group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group);
        AddField("Priority", S["Priority"], DataFieldType.Text, record => record.Priority.ToString(), group);
        AddField("Summary", S["Summary"], DataFieldType.Text, record => record.Summary, group);
        AddField("TerritoryId", S["Territory ID"], DataFieldType.Text, record => record.TerritoryId, group);
        AddField("CustomerId", S["Customer ID"], DataFieldType.Text, record => record.CustomerId, group);
        AddField("Source", S["Source"], DataFieldType.Text, record => record.Source.ToString(), group);
        AddField("CreatedUtc", S["Made"], DataFieldType.DateTime, record => record.CreatedUtc, group);
        AddField("FirstResponseUtc", S["Answered"], DataFieldType.DateTime, record => record.Sla?.FirstResponseUtc, group);
        AddField("CompletedUtc", S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, group);
        AddField("RespondByUtc", S["Answer by"], DataFieldType.DateTime, record => record.Sla?.RespondByUtc, group);
        AddField("ResolveByUtc", S["Resolve by"], DataFieldType.DateTime, record => record.Sla?.ResolveByUtc, group);
        AddField("ResponseMet", S["Answered in time"], DataFieldType.Boolean, record => FieldServiceMeasures.ResponseMet(record), group);
        AddField("ResolutionMet", S["Resolved in time"], DataFieldType.Boolean, record => FieldServiceMeasures.ResolutionMet(record), group);
        AddField("HoursToComplete", S["Hours to complete"], DataFieldType.Decimal, record => record.CompletedUtc is { } done ? Math.Round((decimal)(done - record.CreatedUtc).TotalHours, 2) : null, group);
        AddField("PartsUsed", S["Parts used"], DataFieldType.Decimal, record => record.Lines.Where(line => line.Kind == WorkOrderLineKind.Product).Sum(line => line.QuantityUsed), group);
        AddField("FollowUpOf", S["Follow-up of"], DataFieldType.Text, record => record.ParentWorkOrderId, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<WorkOrderIndex, DateTime>> Column)? DateColumn => ("CreatedUtc", index => index.CreatedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<WorkOrderIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<WorkOrderIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["TypeId"] = index => index.TypeId,
            ["Status"] = index => index.Status,
            ["TerritoryId"] = index => index.TerritoryId,
        };
}

/// <summary>
/// One row per visit: who, when, how long the trip and the time on site were, whether they arrived in the window and
/// whether the job was done the first time.
/// </summary>
public sealed class VisitsReportDataSet : IndexedReportDataSet<ServiceVisit, ServiceVisitIndex>, IFieldServiceReportDataSet
{
    private readonly IStaffMemberManager _staffManager;
    private Dictionary<string, StaffMember> _staff = new(StringComparer.Ordinal);

    /// <summary>
    /// The data set's name.
    /// </summary>
    public const string DataSetName = "FieldServiceVisits";

    /// <summary>
    /// Initializes a new instance of the <see cref="VisitsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VisitsReportDataSet(ISession session, IAuthorizationService authorizationService, IStaffMemberManager staffManager, IStringLocalizer<VisitsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Visits"], stringLocalizer["One row per visit: the technician, start and end, travel, arrival against the window, time on site, and whether the job was done the first time."]), session, authorizationService, DispatchConstants.CollectionName, DispatchPermissions.ViewDispatch)
    {
        _staffManager = staffManager;
        var S = stringLocalizer;
        var group = S["Visit"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Number", S["Number"], DataFieldType.Text, record => record.Number, group);
        AddField("WorkOrderId", S["Work order ID"], DataFieldType.Text, record => record.WorkOrderId, group);
        AddField("WorkOrderNumber", S["Work order"], DataFieldType.Text, record => record.WorkOrderNumber, group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group);
        AddField("LeadWorkerId", S["Technician ID"], DataFieldType.Text, record => record.LeadWorkerId, group);
        AddField("Technician", S["Technician"], DataFieldType.Text, record => _staff.GetValueOrDefault(record.LeadWorkerId ?? string.Empty)?.DisplayName, group);
        AddField("LaborCost", S["Labor cost"], DataFieldType.Decimal, record => (object)LaborCost(record), group);
        AddField("CrewSize", S["Crew"], DataFieldType.Integer, record => record.WorkerIds.Count, group);
        AddField("TerritoryId", S["Territory ID"], DataFieldType.Text, record => record.TerritoryId, group);
        AddField("StartUtc", S["Start"], DataFieldType.DateTime, record => record.StartUtc, group);
        AddField("EndUtc", S["End"], DataFieldType.DateTime, record => record.EndUtc, group);
        AddField("TravelMinutes", S["Travel minutes"], DataFieldType.Integer, record => record.TravelMinutes, group);
        AddField("TravelKm", S["Travel km"], DataFieldType.Decimal, record => record.TravelKm is { } km ? Math.Round((decimal)km, 1) : null, group);
        AddField("ArrivedUtc", S["Arrived"], DataFieldType.DateTime, record => FieldServiceMeasures.Arrived(record), group);
        AddField("OnTime", S["Arrived in the window"], DataFieldType.Boolean, record => FieldServiceMeasures.OnTime(record), group);
        AddField("OnSiteMinutes", S["Minutes on site"], DataFieldType.Integer, record => FieldServiceMeasures.OnSiteMinutes(record), group);
        AddField("FirstTimeFix", S["Done the first time"], DataFieldType.Boolean, record => record.Status == VisitStatus.Completed ? string.IsNullOrEmpty(record.ReturnVisitId) : null, group);
        AddField("IncompleteReason", S["Why not finished"], DataFieldType.Text, record => record.IncompleteReason, group);

        CompleteDescription();
    }

    /// <summary>
    /// Reads the staff once per run, for the technician names and the cost of their hours.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>No narrowing: every visit the principal may read.</returns>
    protected override async Task<Func<IQueryIndex<ServiceVisitIndex>, IQueryIndex<ServiceVisitIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var (_, people) = await _staffManager.SearchAsync(new StaffQuery(), 0, 5000, cancellationToken);

        _staff = people.ToDictionary(person => person.ItemId, StringComparer.Ordinal);

        return await base.RestrictAsync(context, cancellationToken);
    }

    // The crew's hours on the visit (on site when known, else as planned) at each person's cost of an hour on the day.
    private decimal? LaborCost(ServiceVisit visit)
    {
        var minutes = FieldServiceMeasures.OnSiteMinutes(visit) ?? (visit.StartUtc is { } start && visit.EndUtc is { } end ? (int)Math.Max(0, (end - start).TotalMinutes) : 0);
        var day = DateOnly.FromDateTime(visit.StartUtc ?? visit.CreatedUtc);
        decimal? total = null;

        foreach (var workerId in visit.WorkerIds.Append(visit.LeadWorkerId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (_staff.GetValueOrDefault(workerId) is { } person
                && person.TryGet<StaffPayRates>(out var pay)
                && pay.CostOn(day) is { } hourly)
            {
                total = (total ?? 0) + Math.Round(hourly * minutes / 60m, 2);
            }
        }

        return total;
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<ServiceVisitIndex, DateTime>> Column)? DateColumn => ("CreatedUtc", index => index.CreatedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<ServiceVisitIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ServiceVisitIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["WorkOrderId"] = index => index.WorkOrderId,
            ["LeadWorkerId"] = index => index.LeadWorkerId,
            ["Status"] = index => index.Status,
        };
}

/// <summary>
/// The measures the field service reports and KPIs share.
/// </summary>
public static class FieldServiceMeasures
{
    /// <summary>
    /// Whether a work order was answered by its response time, or <see langword="null"/> without one.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <returns>Whether it was.</returns>
    public static bool? ResponseMet(WorkOrder workOrder)
        => workOrder.Sla?.RespondByUtc is not { } by ? null : workOrder.Sla.FirstResponseUtc is { } at && at <= by;

    /// <summary>
    /// Whether a completed work order was resolved by its resolution time, or <see langword="null"/>.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <returns>Whether it was.</returns>
    public static bool? ResolutionMet(WorkOrder workOrder)
        => workOrder.Sla?.ResolveByUtc is not { } by || workOrder.CompletedUtc is not { } done ? null : done <= by;

    /// <summary>
    /// When the technician arrived, the first time the visit went on site.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <returns>The time, in UTC.</returns>
    public static DateTime? Arrived(ServiceVisit visit)
        => visit.History.FirstOrDefault(change => change.Status == VisitStatus.OnSite)?.Utc;

    /// <summary>
    /// Whether the technician arrived by the end of the arrival window, or <see langword="null"/>.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <returns>Whether they did.</returns>
    public static bool? OnTime(ServiceVisit visit)
        => Arrived(visit) is { } at && (visit.ArrivalEndUtc ?? visit.StartUtc) is { } by ? at <= by.AddMinutes(1) : null;

    /// <summary>
    /// The minutes from arriving to finishing, or <see langword="null"/> when not finished.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <returns>The minutes.</returns>
    public static int? OnSiteMinutes(ServiceVisit visit)
    {
        var finished = visit.History.LastOrDefault(change => change.Status is VisitStatus.Completed or VisitStatus.Incomplete)?.Utc;

        return Arrived(visit) is { } at && finished is { } end && end >= at ? (int)Math.Round((end - at).TotalMinutes) : null;
    }
}
