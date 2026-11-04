using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Dispatch.Core.Indexes;

/// <summary>
/// Indexes visits for the lists, the board and the work order's page.
/// </summary>
public sealed class ServiceVisitIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the visit number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public string WorkOrderId { get; set; }

    /// <summary>
    /// Gets or sets the work order's number.
    /// </summary>
    public string WorkOrderNumber { get; set; }

    /// <summary>
    /// Gets or sets the work order's summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether the visit is still to happen or happening.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets the priority, as a number so it sorts.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the territory.
    /// </summary>
    public string TerritoryId { get; set; }

    /// <summary>
    /// Gets or sets the lead worker.
    /// </summary>
    public string LeadWorkerId { get; set; }

    /// <summary>
    /// Gets or sets the start, in UTC.
    /// </summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end, in UTC.
    /// </summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>
    /// Gets or sets when the work order is due, in UTC.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the visit was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// One row per worker on a scheduled visit, to find who is busy when.
/// </summary>
public sealed class VisitWorkerIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string VisitId { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the start, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end, in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the visit still keeps the worker busy.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Maps visits to their indexes.
/// </summary>
public sealed class ServiceVisitIndexProvider : IndexProvider<ServiceVisit>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceVisitIndexProvider"/> class.
    /// </summary>
    public ServiceVisitIndexProvider()
    {
        CollectionName = DispatchConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ServiceVisit> context)
    {
        context.For<ServiceVisitIndex>()
            .Map(visit => new ServiceVisitIndex
            {
                ItemId = visit.ItemId,
                Number = visit.Number,
                WorkOrderId = visit.WorkOrderId,
                WorkOrderNumber = visit.WorkOrderNumber,
                Summary = visit.Summary?.Length > 255 ? visit.Summary[..255] : visit.Summary,
                Status = visit.Status.ToString(),
                IsOpen = VisitStatusRules.IsOpen(visit.Status),
                Priority = (int)visit.Priority,
                TerritoryId = visit.TerritoryId,
                LeadWorkerId = visit.LeadWorkerId,
                StartUtc = visit.StartUtc,
                EndUtc = visit.EndUtc,
                DueUtc = visit.DueUtc,
                CreatedUtc = visit.CreatedUtc,
            });

        context.For<VisitWorkerIndex>()
            .Map(visit => visit.StartUtc is null || visit.EndUtc is null
                ? []
                : visit.WorkerIds.Distinct(StringComparer.Ordinal).Select(workerId => new VisitWorkerIndex
                {
                    VisitId = visit.ItemId,
                    WorkerId = workerId,
                    StartUtc = visit.StartUtc.Value,
                    EndUtc = visit.EndUtc.Value,
                    IsActive = VisitStatusRules.KeepsWorkersBusy(visit.Status),
                }));
    }
}

/// <summary>
/// Creates the dispatch index tables.
/// </summary>
public static class DispatchIndexSchema
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = DispatchConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<ServiceVisitIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Number", column => column.Nullable().WithLength(32))
            .Column<string>("WorkOrderId", column => column.Nullable().WithLength(26))
            .Column<string>("WorkOrderNumber", column => column.Nullable().WithLength(32))
            .Column<string>("Summary", column => column.Nullable().WithLength(255))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<bool>("IsOpen")
            .Column<int>("Priority")
            .Column<string>("TerritoryId", column => column.Nullable().WithLength(26))
            .Column<string>("LeadWorkerId", column => column.Nullable().WithLength(26))
            .Column<DateTime?>("StartUtc", column => column.Nullable())
            .Column<DateTime?>("EndUtc", column => column.Nullable())
            .Column<DateTime?>("DueUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ServiceVisitIndex>(table =>
        {
            table.CreateIndex("IDX_ServiceVisitIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ServiceVisitIndex_WorkOrder", "WorkOrderId", "CreatedUtc");
            table.CreateIndex("IDX_ServiceVisitIndex_Status", "IsOpen", "Status", "Priority");
            table.CreateIndex("IDX_ServiceVisitIndex_Start", "StartUtc", "EndUtc");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<VisitWorkerIndex>(table => table
            .Column<string>("VisitId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.WithLength(26))
            .Column<DateTime>("StartUtc")
            .Column<DateTime>("EndUtc")
            .Column<bool>("IsActive"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<VisitWorkerIndex>(table =>
            table.CreateIndex("IDX_VisitWorkerIndex_Worker", "WorkerId", "IsActive", "StartUtc", "EndUtc"), collection: collection);
    }
}
