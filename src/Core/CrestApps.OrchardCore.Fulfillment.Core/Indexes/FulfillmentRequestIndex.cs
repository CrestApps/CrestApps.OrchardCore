using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Fulfillment.Core.Indexes;

/// <summary>
/// Finds fulfillment requests by order, by partner and by what is due.
/// </summary>
public sealed class FulfillmentRequestIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the order identifier.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the partner's technical name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets when the request may be sent next.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets when the request last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}

/// <summary>
/// Maps fulfillment requests.
/// </summary>
public sealed class FulfillmentRequestIndexProvider : IndexProvider<FulfillmentRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentRequestIndexProvider"/> class.
    /// </summary>
    public FulfillmentRequestIndexProvider()
    {
        CollectionName = FulfillmentConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<FulfillmentRequest> context)
    {
        context.For<FulfillmentRequestIndex>()
            .Map(item => new FulfillmentRequestIndex
            {
                ItemId = item.ItemId,
                OrderId = item.OrderId,
                ProviderName = item.ProviderName,
                Status = item.Status.ToString(),
                NextAttemptUtc = item.NextAttemptUtc,
                ModifiedUtc = item.ModifiedUtc,
            });
    }
}

/// <summary>
/// Creates the fulfillment index tables.
/// </summary>
public static class FulfillmentIndexSchema
{
    /// <summary>
    /// Creates the tables and their indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = FulfillmentConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<FulfillmentRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.Nullable().WithLength(26))
            .Column<string>("ProviderName", column => column.Nullable().WithLength(64))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<DateTime>("NextAttemptUtc")
            .Column<DateTime>("ModifiedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<FulfillmentRequestIndex>(table =>
        {
            table.CreateIndex("IDX_FulfillmentRequestIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_FulfillmentRequestIndex_Order", "OrderId", "ProviderName");
            table.CreateIndex("IDX_FulfillmentRequestIndex_Due", "Status", "NextAttemptUtc");
        }, collection: collection);
    }
}
