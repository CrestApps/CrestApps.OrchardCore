using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Dispatch.Core.Indexes;

/// <summary>
/// Indexes the records of commands from the technician app, to find a command sent again.
/// </summary>
public sealed class FieldCommandReceiptIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the command's key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the worker who sent it.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string VisitId { get; set; }

    /// <summary>
    /// Gets or sets when it came in, in UTC.
    /// </summary>
    public DateTime ReceivedUtc { get; set; }
}

/// <summary>
/// Indexes workers' location consent.
/// </summary>
public sealed class WorkerLocationConsentIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the version of the notice agreed to, or <see langword="null"/>.
    /// </summary>
    public string NoticeVersion { get; set; }
}

/// <summary>
/// Indexes technicians' positions, with the position, so the board reads the index only.
/// </summary>
public sealed class WorkerLocationPingIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets when the position was taken, in UTC.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets the latitude.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string VisitId { get; set; }
}

/// <summary>
/// Maps the technician app's records to their indexes.
/// </summary>
public sealed class FieldCommandReceiptIndexProvider : IndexProvider<FieldCommandReceipt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldCommandReceiptIndexProvider"/> class.
    /// </summary>
    public FieldCommandReceiptIndexProvider()
    {
        CollectionName = DispatchConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<FieldCommandReceipt> context)
    {
        context.For<FieldCommandReceiptIndex>()
            .Map(receipt => new FieldCommandReceiptIndex
            {
                ItemId = receipt.ItemId,
                Key = receipt.Key,
                WorkerId = receipt.WorkerId,
                VisitId = receipt.VisitId,
                ReceivedUtc = receipt.ReceivedUtc,
            });
    }
}

/// <summary>
/// Maps location consent to its index.
/// </summary>
public sealed class WorkerLocationConsentIndexProvider : IndexProvider<WorkerLocationConsent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerLocationConsentIndexProvider"/> class.
    /// </summary>
    public WorkerLocationConsentIndexProvider()
    {
        CollectionName = DispatchConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<WorkerLocationConsent> context)
    {
        context.For<WorkerLocationConsentIndex>()
            .Map(consent => new WorkerLocationConsentIndex
            {
                ItemId = consent.ItemId,
                WorkerId = consent.WorkerId,
                NoticeVersion = consent.NoticeVersion,
            });
    }
}

/// <summary>
/// Maps positions to their index.
/// </summary>
public sealed class WorkerLocationPingIndexProvider : IndexProvider<WorkerLocationPing>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerLocationPingIndexProvider"/> class.
    /// </summary>
    public WorkerLocationPingIndexProvider()
    {
        CollectionName = DispatchConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<WorkerLocationPing> context)
    {
        context.For<WorkerLocationPingIndex>()
            .Map(ping => new WorkerLocationPingIndex
            {
                ItemId = ping.ItemId,
                WorkerId = ping.WorkerId,
                Utc = ping.Utc,
                Latitude = ping.Latitude,
                Longitude = ping.Longitude,
                VisitId = ping.VisitId,
            });
    }
}

/// <summary>
/// Creates the index tables of the technician app.
/// </summary>
public static class FieldIndexSchema
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

        await schemaBuilder.CreateMapIndexTableAsync<FieldCommandReceiptIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Key", column => column.WithLength(64))
            .Column<string>("WorkerId", column => column.WithLength(26))
            .Column<string>("VisitId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("ReceivedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<FieldCommandReceiptIndex>(table =>
        {
            table.CreateIndex("IDX_FieldCommandReceiptIndex_Key", "WorkerId", "Key");
            table.CreateIndex("IDX_FieldCommandReceiptIndex_Received", "ReceivedUtc");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<WorkerLocationConsentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.WithLength(26))
            .Column<string>("NoticeVersion", column => column.Nullable().WithLength(32)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WorkerLocationConsentIndex>(table =>
            table.CreateIndex("IDX_WorkerLocationConsentIndex_Worker", "WorkerId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<WorkerLocationPingIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.WithLength(26))
            .Column<DateTime>("Utc")
            .Column<double>("Latitude")
            .Column<double>("Longitude")
            .Column<string>("VisitId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WorkerLocationPingIndex>(table =>
        {
            table.CreateIndex("IDX_WorkerLocationPingIndex_Worker", "WorkerId", "Utc");
            table.CreateIndex("IDX_WorkerLocationPingIndex_Utc", "Utc");
        }, collection: collection);
    }
}
